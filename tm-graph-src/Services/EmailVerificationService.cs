using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using tm_graph_src.Options;
using tm_graph.Services;
using tm_graph.Types;


namespace tm_graph_src.Services;

public class EmailVerificationService(
    EmailService emailService,
    ITypedCache<(string sessionHash, string codeHash, EmailVerificationReason reason), User> pendingUsers,
    ITypedSet<PendingEmail> pendingEmails,
    ITypedSet<PendingUsername> pendingUsernames,
    IOptions<EmailVerificationCodeOptions> options, IHashService hashService)
{

    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private readonly EmailVerificationCodeOptions _options = options.Value;

    /// <summary>
    /// Initiates the email verification process for the specified user and reason.
    /// Generates a verification session and code, stores the hashed data, and sends the verification email to the user.
    /// </summary>
    /// <param name="user">The user object containing email and other necessary details.</param>
    /// <param name="reason">The reason for the email verification (e.g., Registration, AccountEditing, PasswordReset).</param>
    /// <returns>The verification session string, which can be used for further verification actions.</returns>
    /// <exception cref="ArgumentException">Thrown if the user already has a pending verification for the specified reason.</exception>
    /// <exception cref="Exception">Thrown if an issue occurs during email sending or other operations.</exception>
    public async Task<string> InitiateVerification(User user, EmailVerificationReason reason)
    {
        var (session, code) = _generate();
        var (sessionHash, codeHash) = (hashService.Hash(session), hashService.Hash(code));
        await _executeExclusive(async () =>
        {
            if (await IsVerificationPending(user, reason))
                throw new ArgumentException("User is already pending");
            await _setUser(user, sessionHash, codeHash, reason);
        });
        try
        {
            await emailService.SendMessage(
                user.Email,
                $"Your code is {code}",
                "Verification code");
        }
        catch
        {
            await CompleteVerification(code, session, reason);
            throw;
        }
        return session;
    }

    /// <summary>
    /// Completes the email verification process by validating the provided code and session against the stored data.
    /// If valid, removes the verification session and associated user from the pending state.
    /// </summary>
    /// <param name="code">The verification code provided by the user.</param>
    /// <param name="session">The verification session string provided by the user.</param>
    /// <param name="reason">The reason for the email verification (e.g., Registration, AccountEditing, PasswordReset).</param>
    /// <returns>The user associated with the completed verification if successful, or null if the verification fails.</returns>
    public async Task<User?> CompleteVerification(string code, string session, EmailVerificationReason reason)
    {
        var user = await _executeExclusive(async () =>
           await _unsetUser(hashService.Hash(session), hashService.Hash(code), reason));
        return user;
    }


    /// <summary>
    /// Checks if a verification process is currently pending for the specified user and reason.
    /// Determines whether the user's email or username has an ongoing verification request tied to the provided reason.
    /// </summary>
    /// <param name="user">The user object containing email and username details.</param>
    /// <param name="reason">The reason for the email verification (e.g., Registration, AccountEditing, PasswordReset).</param>
    /// <returns>A boolean value indicating whether a verification is pending for the given user and reason.</returns>
    public async Task<bool> IsVerificationPending(User user, EmailVerificationReason reason)
    {
        return await pendingEmails.Has(new(user.Email, reason)) 
               || await pendingUsernames.Has(new (user.Username, reason));
    }

    public async Task<bool> EmailPending(string email, EmailVerificationReason reason)
    {
        return await pendingEmails.Has(new(email, reason));
    }
    
    public async Task<bool> UsernamePending(string username, EmailVerificationReason reason)
    {
        return await pendingUsernames.Has(new(username, reason));
    }

    private (string session, string code) _generate() => (
        session: RandomNumberGenerator.GetString(CryptoConsts.Alphanumeric, CryptoConsts.TokenLength),
        code: RandomNumberGenerator.GetString(CryptoConsts.Digits, CryptoConsts.CodeLength));

    /// <summary>
    /// Executes the provided asynchronous action within a thread-safe, exclusive execution context.
    /// Ensures that only one action can execute at a time.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The asynchronous action to execute exclusively.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of the executed action.</returns>
    private async ValueTask<T> _executeExclusive<T>(Func<Task<T>> action)
    {
        await _lock.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Executes a given asynchronous action exclusively, ensuring that no other operations
    /// can interfere during its execution. Uses a semaphore to enforce an exclusive lock.
    /// </summary>
    /// <param name="action">The asynchronous action to execute exclusively.</param>
    /// <returns>A task representing the completion of the asynchronous operation.</returns>
    private async ValueTask _executeExclusive(Func<Task> action)
    {
        await _lock.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            _lock.Release();
        }
    }


    private async ValueTask<User?> _unsetUser(string sessionHash, string codeHash, EmailVerificationReason reason)
    {
        var user = await pendingUsers.PopValue((sessionHash, codeHash, reason));
        if (user is not null)
        {
            await pendingEmails.Unset(new (user.Email, reason));
            await pendingUsernames.Unset(new (user.Username, reason));
        }
        return user;
    }
    
    private async ValueTask _setUser(User user, string sessionHash, string codeHash, EmailVerificationReason reason)
    {
        var ttlOptions = new MemoryCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.TtlSeconds)
        };
        await pendingEmails.Set(new (user.Email, reason), ttlOptions);
        await pendingUsernames.Set(new (user.Username, reason), ttlOptions);
        await pendingUsers.SetValue(
            key: (sessionHash, codeHash, reason), 
            value: user, 
            options: ttlOptions);
    }

}




