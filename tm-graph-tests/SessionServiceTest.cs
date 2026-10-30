using tm_graph_src;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class SessionServiceTest
{
    private readonly FakeSessionStore _store = new();
    private readonly IHashService _hash = new HashService();
    private readonly SessionService _service;

    public SessionServiceTest() => _service = new SessionService(_store, _hash);

    private static User NewUser(string id = "user-1") => new()
    {
        Id = id,
        Username = $"user-{id}",
        Email = $"{id}@example.com",
        Hash = "hash"
    };

    private async Task<(User User, string Token, Session Session)> CreateSessionAsync(string userId = "user-1")
    {
        var user = NewUser(userId);
        var before = _store.Sessions.Count;
        var token = await _service.CreateSession(user, "test-agent");

        Assert.Equal(before + 1, _store.Sessions.Count);
        return (user, token, _store.Sessions[^1]);
    }

    [Fact]
    public async Task CreateSession_ShouldReturnTokenOfExpectedFormat()
    {
        var (_, token, _) = await CreateSessionAsync();

        Assert.Equal(CryptoConsts.TokenLength, token.Length);
        Assert.All(token, c => Assert.Contains(c, CryptoConsts.Alphanumeric));
    }

    [Fact]
    public async Task CreateSession_ShouldPersistSessionWithHashedToken()
    {
        var (user, token, session) = await CreateSessionAsync();

        Assert.NotEqual(token, session.TokenHash);
        Assert.Equal(_hash.Hash(token), session.TokenHash);
        Assert.True(_hash.Verify(token, session.TokenHash));
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal("test-agent", session.UserAgent);
    }

    [Fact]
    public async Task CreateSession_ShouldGenerateUniqueTokens()
    {
        var first = await _service.CreateSession(NewUser("user-1"), "agent-1");
        var second = await _service.CreateSession(NewUser("user-2"), "agent-2");

        Assert.NotEqual(first, second);
        Assert.Equal(2, _store.Sessions.Count);
    }

    [Fact]
    public async Task RevokeSession_ShouldRemoveSession()
    {
        var (user, token, _) = await CreateSessionAsync();

        await _service.RevokeSession(user, token);

        Assert.Empty(_store.Sessions);
    }

    [Fact]
    public async Task RevokeSession_ShouldPassTokenHashInsteadOfPlaintext()
    {
        var (user, token, _) = await CreateSessionAsync();

        await _service.RevokeSession(user, token);

        var revoked = Assert.Single(_store.RevokedByTokenHash);
        Assert.Equal(user.Id, revoked.UserId);
        Assert.Equal(_hash.Hash(token), revoked.TokenHash);
        Assert.NotEqual(token, revoked.TokenHash);
    }

    [Fact]
    public async Task RevokeSession_ShouldNotAffectSessionsOfOtherUsers()
    {
        var (_, otherToken, _) = await CreateSessionAsync("user-2");
        var (user, token, _) = await CreateSessionAsync("user-1");

        await _service.RevokeSession(user, token);

        var remaining = Assert.Single(_store.Sessions);
        Assert.Equal("user-2", remaining.UserId);
        Assert.Equal(_hash.Hash(otherToken), remaining.TokenHash);
    }

    [Fact]
    public async Task RevokeSessionById_ShouldRemoveSession()
    {
        var (user, _, session) = await CreateSessionAsync();

        await _service.RevokeSessionById(user, session.Id);

        Assert.Empty(_store.Sessions);
    }

    [Fact]
    public async Task RevokeSessionById_ShouldNotRemoveSessionOfAnotherUser()
    {
        var (_, _, otherSession) = await CreateSessionAsync("user-2");
        var user = NewUser("user-1");

        await _service.RevokeSessionById(user, otherSession.Id);

        var remaining = Assert.Single(_store.Sessions);
        Assert.Equal("user-2", remaining.UserId);
    }
}
