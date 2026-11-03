using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using tm_graph_src.Exceptions;
using tm_graph_src.Extensions;
using tm_graph_src.Options;
using tm_graph_src.Services;
using tm_graph.Types;

namespace tm_graph_src.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IUserStore userStore, EmailVerificationService emailVerificationService, UniquenessVerifierService uniquenessVerifierService, PasswordHashService passwordHashService, IOptions<EmailVerificationCodeOptions> options) : ControllerBase
{
    public const string RegisterCookieName = "X-Register-Verification-Token";

    private const string ConfirmPath = "/api/auth/confirm";
    private const string LoginErrorMessage = "Wrong identifier or password";

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto userDto)
    {
        var username = userDto.Username.Trim();
        var email = userDto.Email.Trim().ToLowerInvariant();
        var normalized = userDto with { Username = username, Email = email };

        await uniquenessVerifierService.UserUnique(normalized, EmailVerificationReason.Registration);
        var user = new User()
        {
            Username = username,
            Email = email,
            Hash = passwordHashService.Hash(userDto.Password)
        };
        var session = await emailVerificationService.InitiateVerification(user, EmailVerificationReason.Registration);
        Response.Cookies.Append(RegisterCookieName, session, new CookieOptions()
        {
            Expires = DateTimeOffset.UtcNow.AddSeconds(options.Value.TtlSeconds),
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = ConfirmPath
        });
        return Accepted();
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmDto confirmDto)
    {
        var session = Request.Cookies[RegisterCookieName] ?? "";
        var user = await emailVerificationService.PeekVerification(
            confirmDto.Code,
            session,
            EmailVerificationReason.Registration);
        if (user is null) throw new BadRequestException();

        await userStore.AddAsync(user);
        await emailVerificationService.CompleteVerification(
            confirmDto.Code,
            session,
            EmailVerificationReason.Registration);
        await HttpContext.UserSignIn(user, confirmDto.RememberMe);
        return Ok(new PublicUserDto(user.Id, user.Username, user.Email));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var identifier = loginDto.Identifier.Trim();
        User? user;
        if (Regex.IsMatch(identifier, RegularExpressions.EmailRegex))
        {
            user = await userStore.GetUserByEmail(identifier.ToLowerInvariant());
        }
        else if (Regex.IsMatch(identifier, RegularExpressions.UsernameRegex))
        {
            user = await userStore.GetUserByUsername(identifier);
        }
        else
        {
            throw new BadRequestException("Invalid identifier");
        }

        var hash = user?.Hash ?? passwordHashService.DummyHash;
        var passwordMatches = passwordHashService.Verify(hash, loginDto.Password);
        if (user is null || !passwordMatches) throw new BadRequestException(LoginErrorMessage);
        // await HttpContext.UserSignOut();
        await HttpContext.UserSignIn(user, loginDto.RememberMe);
        return Ok(new PublicUserDto(user.Id, user.Username, user.Email));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.UserSignOut();
        return NoContent();
    }
}

public record CreateUserDto(
    [MaxLength(24)]
    [MinLength(3)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Invalid username")]
    string Username,
    
    [MaxLength(64)]
    [MinLength(8)]
    [EmailAddress] 
    string Email,
    
    [MaxLength(64)]
    [MinLength(8)]
    string Password
    
);

public record LoginDto(
    [MaxLength(64)]
    [MinLength(3)]
    string Identifier,
    [MaxLength(64)]
    [MinLength(8)]
    string Password,
    [property: JsonPropertyName("remember_me")]
    bool RememberMe = false
);

public record ConfirmDto(
    [Required]
    [RegularExpression(@"^[0-9]{12}$", ErrorMessage = "Invalid code")]
    string Code,
    [property: JsonPropertyName("remember_me")]
    bool RememberMe = false
);

public record PublicUserDto(
    string Id,
    string Username,
    string Email
    );
