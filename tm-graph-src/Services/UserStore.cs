using Microsoft.EntityFrameworkCore;
using tm_graph_src.Exceptions;

namespace tm_graph_src.Services;

public interface IUserStore
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    Task<User> GetUserByEmail(string email, string errorMessage);
    Task<User> GetUserByUsername(string username,  string errorMessage);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public class UserStore(AppDbContext dbContext) : IUserStore
{
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(u => u.Username == username, cancellationToken);

    public async Task<User> GetUserByEmail(string email, string errorMessage = "User with this email doesn't exist")
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email) ?? throw new NotFoundException(errorMessage);
    }
    
    
    public async Task<User> GetUserByUsername(string username, string errorMessage = "User with this username doesn't exist")
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username) ?? throw new NotFoundException(errorMessage);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
