using Microsoft.EntityFrameworkCore;

namespace tm_graph_src.Services;

public interface ISessionStore
{
    Task AddAsync(Session session, CancellationToken cancellationToken = default);

    Task<bool> RevokeByTokenHashAsync(string userId, string tokenHash, CancellationToken cancellationToken = default);

    Task<bool> RevokeByIdAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
}

public class SessionStore(AppDbContext dbContext) : ISessionStore
{
    public async Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        await dbContext.Sessions.AddAsync(session, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeByTokenHashAsync(string userId, string tokenHash, CancellationToken cancellationToken = default)
    {
        var removed = await dbContext.Sessions
            .Where(s => s.UserId == userId && s.TokenHash == tokenHash)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    public async Task<bool> RevokeByIdAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        var removed = await dbContext.Sessions
            .Where(s => s.UserId == userId && s.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }
}
