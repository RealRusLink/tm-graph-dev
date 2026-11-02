namespace tm_graph_src.Services;

public class SessionService(ISessionStore sessionStore, IHashService hashService)
{
    public async Task<(string Token, DateTimeOffset ExpiresAt)> CreateSession(
        string userId,
        string userAgent,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var token = System.Security.Cryptography.RandomNumberGenerator.GetString(CryptoConsts.Alphanumeric, CryptoConsts.TokenLength);
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var session = new Session()
        {
            TokenHash = hashService.Hash(token),
            UserId = userId,
            UserAgent = userAgent,
            ExpiresAt = expiresAt
        };
        await sessionStore.AddAsync(session, cancellationToken);
        return (token, expiresAt);
    }

    public async Task RevokeSession(string session, CancellationToken cancellationToken = default) =>
        await sessionStore.RevokeByTokenHashAsync(hashService.Hash(session), cancellationToken);

    public async Task RevokeSessionById(User user, string sessionId, CancellationToken cancellationToken = default) =>
        await sessionStore.RevokeByIdAsync(user.Id, sessionId, cancellationToken);

    public async Task<Session?> GetSession(string sessionToken)
    {
        return await sessionStore.GetSession(hashService.Hash(sessionToken));
    }
    
}
