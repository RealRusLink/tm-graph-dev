using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using tm_graph.Services;
using tm_graph.Types;
using tm_graph_src;
using tm_graph_src.Options;
using tm_graph_src.Services;

namespace tm_graph_tests;

internal sealed class FakeEmailService : IEmailService
{
    public List<(string To, string Text, string Subject)> Messages { get; } = new();

    public Exception? FailWith { get; set; }

    public Task SendMessage(string emailTo, string messageText, string messageSubject)
    {
        if (FailWith is not null) throw FailWith;

        Messages.Add((emailTo, messageText, messageSubject));
        return Task.CompletedTask;
    }
}

internal sealed class FakeUserStore : IUserStore
{
    public List<User> Users { get; } = new();

    public int AddCalls { get; private set; }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => u.Email == email));

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => u.Username == username));

    public Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

    public Task<User?> GetUserByUsername(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Username == username));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        AddCalls++;
        Users.Add(user);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSessionStore : ISessionStore
{
    public List<Session> Sessions { get; } = new();

    public List<string> RevokedByTokenHash { get; } = new();

    public List<(string UserId, string SessionId)> RevokedById { get; } = new();

    public Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<bool> RevokeByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        RevokedByTokenHash.Add(tokenHash);
        return Task.FromResult(Sessions.RemoveAll(s => s.TokenHash == tokenHash) > 0);
    }

    public Task<bool> RevokeByIdAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        RevokedById.Add((userId, sessionId));
        return Task.FromResult(Sessions.RemoveAll(s => s.UserId == userId && s.Id == sessionId) > 0);
    }

    public Task<Session?> GetSession(string tokenHash) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.TokenHash == tokenHash));
}

internal sealed class FakeAuthenticationService : IAuthenticationService
{
    public List<(string? Scheme, ClaimsPrincipal Principal, AuthenticationProperties? Properties)> SignIns { get; } = new();

    public List<string?> SignOuts { get; } = new();

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal,
        AuthenticationProperties? properties)
    {
        SignIns.Add((scheme, principal, properties));
        return Task.CompletedTask;
    }

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        SignOuts.Add(scheme);
        return Task.CompletedTask;
    }
}

internal static class TestServices
{
    private static readonly Regex CodeRegex = new(@"\d+");

    public static (DefaultHttpContext Context, FakeAuthenticationService Auth) CreateHttpContext()
    {
        var schemeProvider = new AuthenticationSchemeProvider(
            Options.Create(new AuthenticationOptions { DefaultScheme = SessionAuthenticationOptions.SchemeName }));
        schemeProvider.AddScheme(new AuthenticationScheme(
            SessionAuthenticationOptions.SchemeName,
            displayName: null,
            handlerType: typeof(SessionAuthenticationHandler)));

        var auth = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationSchemeProvider>(schemeProvider);
        services.AddSingleton<IAuthenticationService>(auth);

        return (new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }, auth);
    }

    public static (EmailVerificationService Service, FakeEmailService Email) CreateEmailVerification(int ttlSeconds = 60)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var email = new FakeEmailService();
        var service = new EmailVerificationService(
            email,
            new TypedCacheService<(string sessionHash, string codeHash, EmailVerificationReason reason), User>(cache),
            new TypedSetService<PendingEmail>(new TypedCacheService<PendingEmail, bool>(cache)),
            new TypedSetService<PendingUsername>(new TypedCacheService<PendingUsername, bool>(cache)),
            Options.Create(new EmailVerificationCodeOptions { TtlSeconds = ttlSeconds }),
            new HashService());

        return (service, email);
    }

    public static string ExtractCode(FakeEmailService email)
    {
        var message = email.Messages[^1];
        var match = CodeRegex.Match(message.Text);
        Assert.True(match.Success, $"В письме не найден код: '{message.Text}'");
        return match.Value;
    }
}
