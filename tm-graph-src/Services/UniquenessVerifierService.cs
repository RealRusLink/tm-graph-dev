using tm_graph_src.Controllers;
using tm_graph_src.Exceptions;
using tm_graph.Types;

namespace tm_graph_src.Services;

public class UniquenessVerifierService(IUserStore userStore, EmailVerificationService emailVerificationService)
{
    public async Task UserUnique(CreateUserDto userDto, EmailVerificationReason reason)
    {
        var emailRegistered = await userStore.EmailExistsAsync(userDto.Email);
        var usernameRegistered = await userStore.UsernameExistsAsync(userDto.Username);
        var emailPending = await emailVerificationService.EmailPending(userDto.Email, reason);
        var usernamePending = await emailVerificationService.UsernamePending(userDto.Username, reason);

        if (emailRegistered) throw new ConflictException("User with this email already exists");
        if (usernameRegistered) throw new ConflictException("User with this username already exists");
        if (emailPending) throw new ConflictException("User with this email is already pending");
        if (usernamePending) throw new ConflictException("User with this username is already pending");
    }
}
