using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace tm_graph_src.Services;

public class SessionService(AppDbContext appDbContext, IHashService hashService)
{
    public async Task<string> CreateSession(User user, string userAgent)
    {
        var token = RandomNumberGenerator.GetString(CryptoConsts.Alphanumeric, CryptoConsts.TokenLength);
        var session = new Session()
        {
            TokenHash = hashService.Hash(token),
            UserId = user.Id,
            UserAgent = userAgent
        };
        await appDbContext.Sessions.AddAsync(session);
        return token;
    }

    public async Task RevokeSession(User user, string session)
    {
        var hash = hashService.Hash(session);   
        var target = from s in appDbContext.Sessions
            where s.UserId == user.Id && s.TokenHash == hash
            select s;
        await target.ExecuteDeleteAsync();
    }
    
    public async Task RevokeSessionById(User user, string sessionId)
    {
        var target = from s in appDbContext.Sessions
            where s.UserId == user.Id && s.Id == sessionId
            select s;
        await target.ExecuteDeleteAsync();
    }
    
}