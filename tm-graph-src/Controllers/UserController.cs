using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using tm_graph_src.Exceptions;
using tm_graph_src.Options;
using tm_graph_src.Services;
using tm_graph.Types;

namespace tm_graph_src.Controllers;
[ApiController]
[Route("api/[controller]")]
public class UserController(IUserStore userStore, EmailVerificationService emailVerificationService, UniquenessVerifierService uniquenessVerifierService, PasswordHashService passwordHashService, IOptions<EmailVerificationCodeOptions> options) : ControllerBase
{
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
        Response.Cookies.Append("X-Register-Verification-Token", session, new CookieOptions()
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
        var session = Request.Cookies["X-Register-Verification-Token"] ?? "";
        var user = await emailVerificationService.CompleteVerification(code, session, EmailVerificationReason.Registration);
        if (user is null) throw new BadRequestException();
        await userStore.AddAsync(user);
        return Ok(user);
    }

    
}

public record CreateUserDto(
    [MaxLength(24)]
    [MinLength(3)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$")]
    string Username,
    
    [MaxLength(64)]
    [MinLength(8)]
    [EmailAddress] 
    string Email,
    
    [MaxLength(64)]
    [MinLength(8)]
    string Password
    
);