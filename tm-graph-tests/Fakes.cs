using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
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

    public List<(string UserId, string TokenHash)> RevokedByTokenHash { get; } = new();

    public List<(string UserId, string SessionId)> RevokedById { get; } = new();

    public Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<bool> RevokeByTokenHashAsync(string userId, string tokenHash, CancellationToken cancellationToken = default)
    {
        RevokedByTokenHash.Add((userId, tokenHash));
        return Task.FromResult(Sessions.RemoveAll(s => s.UserId == userId && s.TokenHash == tokenHash) > 0);
    }

    public Task<bool> RevokeByIdAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        RevokedById.Add((userId, sessionId));
        return Task.FromResult(Sessions.RemoveAll(s => s.UserId == userId && s.Id == sessionId) > 0);
    }
}

internal static class TestServices
{
    private static readonly Regex CodeRegex = new(@"\d+");

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
