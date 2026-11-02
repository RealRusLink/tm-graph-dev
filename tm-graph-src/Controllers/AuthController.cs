using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
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

    private const string _registerCookieName = "X-Register-Verification-Token";
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto userDto)
    {
        await uniquenessVerifierService.UserUnique(userDto, EmailVerificationReason.Registration);
        var user = new User()
        {
            Username = userDto.Username,
            Email = userDto.Email,
            Hash = passwordHashService.Hash(userDto.Password)
        };
        var session = await emailVerificationService.InitiateVerification(user, EmailVerificationReason.Registration);
        Response.Cookies.Append(_registerCookieName, session, new CookieOptions()
        {
            Expires = DateTimeOffset.UtcNow.AddSeconds(options.Value.TtlSeconds),
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
        return Ok();
    }
    
    [HttpPost("confirm")]
    public async Task<IActionResult> CheckByCode([FromQuery] string code)
    {
        var session = Request.Cookies[_registerCookieName] ?? "";
        var user = await emailVerificationService.CompleteVerification(code, session, EmailVerificationReason.Registration);
        if (user is null) throw new BadRequestException();
        await userStore.AddAsync(user);
        await HttpContext.UserSignIn(user);
        return Ok(new PublicUserDto(user.Username, user.Email));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        const string errorMessage = "Wrong identifier or password";
        User user;
        if (Regex.IsMatch(loginDto.Identifier, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            user = await userStore.GetUserByEmail(loginDto.Identifier, errorMessage);
        } else if (Regex.IsMatch(loginDto.Identifier, @"^[a-zA-Z0-9_]+$"))
        {
            user = await userStore.GetUserByUsername(loginDto.Identifier, errorMessage);
        }
        else
        {
            throw new BadRequestException("Invalid identifier");
        }

        if (passwordHashService.Verify(user.Hash, loginDto.Password))
        {
            await HttpContext.UserSignIn(user);
            return Ok();
        } 
        throw new BadRequestException(errorMessage);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.UserSignOut();
        return Ok();
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
    string Password
);

public record PublicUserDto(
    string Username,
    string Email
    );