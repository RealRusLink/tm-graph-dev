using Microsoft.EntityFrameworkCore;
using tm_graph_src.Controllers;
using tm_graph_src.Exceptions;
using tm_graph.Types;

namespace tm_graph_src.Services;

public class UniquenessVerifierService(AppDbContext appDbContext, EmailVerificationService emailVerificationService)
{
    
    public async Task UserUnique(CreateUserDto userDto, EmailVerificationReason reason)
    {
        
        var query = from u in appDbContext.Users
            where u.Email == userDto.Email || u.Username == userDto.Username
            select new {emailExists = u.Email == userDto.Email, usernameExists = u.Username == userDto.Username};

        var registered = query.Aggregate((a, item) => new
            {
                emailExists = a.emailExists || item.emailExists,
                usernameExists = a.usernameExists || item.usernameExists
            });
        var emailPending = await emailVerificationService.EmailPending(userDto.Email, reason);
        var usernamePending = await emailVerificationService.UsernamePending(userDto.Username, reason);
        if (registered.emailExists) throw new ConflictException("User with this email already exists");
        if (registered.usernameExists) throw new ConflictException("User with this username already exists");
        if (emailPending) throw new ConflictException("User with this email is already pending");
        if (usernamePending) throw new ConflictException("User with this username is already pending");
    }
}