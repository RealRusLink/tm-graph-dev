
namespace tm_graph_src.Services;

public class SessionService(ISessionStore sessionStore, IHashService hashService)
{
    public async Task<string> CreateSession(string userId, string userAgent, CancellationToken cancellationToken = default)
    {
        var token = System.Security.Cryptography.RandomNumberGenerator.GetString(CryptoConsts.Alphanumeric, CryptoConsts.TokenLength);
        var session = new Session()
        {
            TokenHash = hashService.Hash(token),
            UserId = userId,
            UserAgent = userAgent
        };
        await sessionStore.AddAsync(session, cancellationToken);
        return token;
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
