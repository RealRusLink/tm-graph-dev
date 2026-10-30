using System.Security.Cryptography;

namespace tm_graph_src.Services;

public class SessionService(ISessionStore sessionStore, IHashService hashService)
{
    public async Task<string> CreateSession(User user, string userAgent, CancellationToken cancellationToken = default)
    {
        var token = RandomNumberGenerator.GetString(CryptoConsts.Alphanumeric, CryptoConsts.TokenLength);
        var session = new Session()
        {
            TokenHash = hashService.Hash(token),
            UserId = user.Id,
            UserAgent = userAgent
        };
        await sessionStore.AddAsync(session, cancellationToken);
        return token;
    }

    public Task RevokeSession(User user, string session, CancellationToken cancellationToken = default) =>
        sessionStore.RevokeByTokenHashAsync(user.Id, hashService.Hash(session), cancellationToken);

    public Task RevokeSessionById(User user, string sessionId, CancellationToken cancellationToken = default) =>
        sessionStore.RevokeByIdAsync(user.Id, sessionId, cancellationToken);
}
