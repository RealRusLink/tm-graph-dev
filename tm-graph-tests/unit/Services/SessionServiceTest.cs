using Microsoft.EntityFrameworkCore;
using tm_graph_src;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class SessionServiceTest : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly IHashService _hash = new HashService();

    public void Dispose() => _database.Dispose();

    private static User NewUser(string id = "user-1") => new()
    {
        Id = id,
        Username = $"user-{id}",
        Email = $"{id}@example.com",
        Hash = "hash"
    };

    private static async Task AddUserAsync(AppDbContext db, User user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task<(User User, string Token, Session Session)> CreateSessionAsync(
        AppDbContext db,
        string userId = "user-1")
    {
        var user = NewUser(userId);
        await AddUserAsync(db, user);
        var service = new SessionService(db, _hash);
        var (token, _) = await service.CreateSession(user.Id, "test-agent", TimeSpan.FromDays(1));
        var session = await db.Sessions.SingleAsync(s => s.UserId == user.Id);

        return (user, token, session);
    }

    [Fact]
    public async Task CreateSession_ShouldReturnTokenOfExpectedFormat()
    {
        await using var db = _database.CreateContext();
        var (_, token, _) = await CreateSessionAsync(db);

        Assert.Equal(CryptoConsts.TokenLength, token.Length);
        Assert.All(token, c => Assert.Contains(c, CryptoConsts.Alphanumeric));
    }

    [Fact]
    public async Task CreateSession_ShouldPersistSessionWithHashedToken()
    {
        await using var db = _database.CreateContext();
        var (user, token, session) = await CreateSessionAsync(db);

        Assert.NotEqual(token, session.TokenHash);
        Assert.Equal(_hash.Hash(token), session.TokenHash);
        Assert.True(_hash.Verify(token, session.TokenHash));
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal("test-agent", session.UserAgent);
    }

    [Fact]
    public async Task CreateSession_ShouldGenerateUniqueTokens()
    {
        await using var db = _database.CreateContext();
        var service = new SessionService(db, _hash);
        var firstUser = NewUser("user-1");
        var secondUser = NewUser("user-2");
        await AddUserAsync(db, firstUser);
        await AddUserAsync(db, secondUser);

        var (first, _) = await service.CreateSession(firstUser.Id, "agent-1", TimeSpan.FromDays(1));
        var (second, _) = await service.CreateSession(secondUser.Id, "agent-2", TimeSpan.FromDays(1));

        Assert.NotEqual(first, second);
        Assert.Equal(2, await db.Sessions.CountAsync());
    }

    [Fact]
    public async Task CreateSession_ShouldSetExpiryFromLifetime()
    {
        await using var db = _database.CreateContext();
        var service = new SessionService(db, _hash);
        var user = NewUser();
        await AddUserAsync(db, user);
        var before = DateTimeOffset.UtcNow;

        var (_, expiresAt) = await service.CreateSession(user.Id, "test-agent", TimeSpan.FromHours(3));

        var session = await db.Sessions.SingleAsync();
        Assert.Equal(expiresAt, session.ExpiresAt);
        Assert.InRange(session.ExpiresAt, before.AddHours(3), DateTimeOffset.UtcNow.AddHours(3));
    }

    [Fact]
    public async Task RevokeSession_ShouldRemoveSession()
    {
        await using var db = _database.CreateContext();
        var (_, token, _) = await CreateSessionAsync(db);

        await new SessionService(db, _hash).RevokeSession(token);

        Assert.False(await db.Sessions.AnyAsync());
    }

    [Fact]
    public async Task RevokeSession_ShouldPassTokenHashInsteadOfPlaintext()
    {
        await using var db = _database.CreateContext();
        var (_, token, _) = await CreateSessionAsync(db, "user-1");
        var (_, otherToken, _) = await CreateSessionAsync(db, "user-2");

        await new SessionService(db, _hash).RevokeSession(token);

        var remaining = await db.Sessions.SingleAsync();
        Assert.Equal(_hash.Hash(otherToken), remaining.TokenHash);
        Assert.NotEqual(otherToken, remaining.TokenHash);
    }

    [Fact]
    public async Task RevokeSession_ShouldNotAffectSessionsOfOtherUsers()
    {
        await using var db = _database.CreateContext();
        var (_, otherToken, _) = await CreateSessionAsync(db, "user-2");
        var (_, token, _) = await CreateSessionAsync(db, "user-1");

        await new SessionService(db, _hash).RevokeSession(token);

        var remaining = await db.Sessions.SingleAsync();
        Assert.Equal("user-2", remaining.UserId);
        Assert.Equal(_hash.Hash(otherToken), remaining.TokenHash);
    }

    [Fact]
    public async Task RevokeSessionById_ShouldRemoveSession()
    {
        await using var db = _database.CreateContext();
        var (user, _, session) = await CreateSessionAsync(db);

        await new SessionService(db, _hash).RevokeSessionById(user, session.Id);

        Assert.False(await db.Sessions.AnyAsync());
    }

    [Fact]
    public async Task RevokeSessionById_ShouldNotRemoveSessionOfAnotherUser()
    {
        await using var db = _database.CreateContext();
        var (_, _, otherSession) = await CreateSessionAsync(db, "user-2");
        var user = NewUser("user-1");

        await new SessionService(db, _hash).RevokeSessionById(user, otherSession.Id);

        var remaining = await db.Sessions.SingleAsync();
        Assert.Equal("user-2", remaining.UserId);
    }

    [Fact]
    public async Task GetSession_ShouldReturnSession_ForValidToken()
    {
        await using var db = _database.CreateContext();
        var (user, token, session) = await CreateSessionAsync(db);

        var found = await new SessionService(db, _hash).GetSession(token);

        Assert.NotNull(found);
        Assert.Equal(session.Id, found.Id);
        Assert.Equal(session.TokenHash, found.TokenHash);
        Assert.Equal(session.UserId, found.UserId);
        Assert.Equal(session.UserAgent, found.UserAgent);
        Assert.Equal(session.ExpiresAt, found.ExpiresAt);
        Assert.Equal(user.Id, found.UserId);
        Assert.NotNull(found.User);
        Assert.Equal(user.Id, found.User.Id);
    }

    [Fact]
    public async Task GetSession_ShouldHashTokenBeforeLookup()
    {
        await using var db = _database.CreateContext();
        var (_, token, _) = await CreateSessionAsync(db);
        var service = new SessionService(db, _hash);

        Assert.NotNull(await service.GetSession(token));
        Assert.Null(await service.GetSession(token.ToUpperInvariant()));
        Assert.Null(await service.GetSession("unknown-token"));
    }
}
