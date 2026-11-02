using tm_graph_src.Controllers;
using tm_graph_src.Exceptions;
using tm_graph.Types;

namespace tm_graph_src.Services;

public class UniquenessVerifierService(IUserStore userStore, EmailVerificationService emailVerificationService)
{
    /// <summary>
    /// Verifies the uniqueness of a user's email and username against the registered and pending entries.
    /// </summary>
    /// <param name="userDto">
    /// The data transfer object representing the user, including properties such as Username and Email.
    /// </param>
    /// <param name="reason">
    /// The reason for email verification, specified as an <see cref="EmailVerificationReason"/> enum value.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. If any conflict is detected, an exception is thrown.
    /// </returns>
    /// <exception cref="ConflictException">
    /// Thrown if an email or username is already registered or is pending verification.
    /// </exception>
    public async Task UserUnique(CreateUserDto userDto, EmailVerificationReason reason)
    {
        await EmailUnique(userDto.Email, reason);
        await UserNameUnique(userDto.Username, reason);
    }

    public async Task UserNameUnique(string username, EmailVerificationReason reason)
    {
        var usernameRegistered = await userStore.UsernameExistsAsync(username);
        if (usernameRegistered) throw new ConflictException("User with this username already exists");
        var usernamePending = await emailVerificationService.UsernamePending(username, reason);
        if (usernamePending) throw new ConflictException("User with this username is already pending");
    }

    public async Task EmailUnique(string email, EmailVerificationReason reason)
    {
        var emailRegistered = await userStore.EmailExistsAsync(email);
        if (emailRegistered) throw new ConflictException("User with this email already exists");
        var emailPending = await emailVerificationService.EmailPending(email, reason);
        if (emailPending) throw new ConflictException("User with this email is already pending");
    }


}


