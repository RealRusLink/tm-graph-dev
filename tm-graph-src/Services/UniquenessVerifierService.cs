using tm_graph_src.Controllers;
using tm_graph_src.Exceptions;
using tm_graph.Types;

namespace tm_graph_src.Services;

public class UniquenessVerifierService(IUserStore userStore, EmailVerificationService emailVerificationService)
{
    /// <summary>
    /// Verifies the uniqueness of a user's email and username against the registered and pending entries.
    /// </summary>
    /// <param name="userDto">
    /// The data transfer object representing the user, including properties such as Username and Email.
    /// </param>
    /// <param name="reason">
    /// The reason for email verification, specified as an <see cref="EmailVerificationReason"/> enum value.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. If any conflict is detected, an exception is thrown.
    /// </returns>
    /// <exception cref="ConflictException">
    /// Thrown if an email or username is already registered or is pending verification.
    /// </exception>
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
