using Microsoft.EntityFrameworkCore;

namespace tm_graph_src.Services;

public class SessionService(AppDbContext appDbContext, IHashService hashService)
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
        // await sessionStore.AddAsync(session, cancellationToken);
        await appDbContext.Sessions.AddAsync(session);
        await appDbContext.SaveChangesAsync();
        return (token, expiresAt);
    }

    public async Task RevokeSession(string session, CancellationToken cancellationToken = default)
    {
        await appDbContext.Sessions
        .Where(s => s.TokenHash == hashService.Hash(session)).ExecuteDeleteAsync();
    }

    public async Task RevokeSessionById(User user, string sessionId, CancellationToken cancellationToken = default)
    {
        await appDbContext.Sessions.Where(s => s.UserId == user.Id && s.Id == sessionId).ExecuteDeleteAsync();
    }

    public async Task<Session?> GetSession(string sessionToken)
    {
        return await appDbContext.Sessions.FirstOrDefaultAsync(s => s.TokenHash == hashService.Hash(sessionToken));
    }

}
