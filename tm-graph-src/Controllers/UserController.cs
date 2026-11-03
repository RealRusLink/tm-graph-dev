using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tm_graph_src;
using tm_graph_src.Exceptions;
using tm_graph_src.Options;
using tm_graph_src.Services;
using tm_graph.Types;

namespace tm_graph_src.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController(AppDbContext appDbContext, UniquenessVerifierService uniquenessVerifierService, EmailVerificationService emailVerificationService, IOptions<EmailVerificationCodeOptions> emailCodeOptions, PasswordHashService passwordHashService) : ControllerBase
    {
        
        public const string VerificationCookieName = "X-Verification-Token";

        public const string VerificationPath = "api/user/verification";
        
        [HttpGet("me")]
        public async Task<IActionResult> GetUser(User user)
        {
            return Ok(new PublicUserDto(user.Id, user.Username, user.Email));
        }

        [HttpPost("username")]
        public async Task<IActionResult> ChangeUsername(User user, 
            [FromBody] 
            [RegularExpression(RegularExpressions.UsernameRegex, ErrorMessage = "Invalid username")] 
            string username
            )
        {
            user = await appDbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            await uniquenessVerifierService.UserNameUnique(username, EmailVerificationReason.Registration);
            user.Username = username;
            await appDbContext.SaveChangesAsync();
            return Ok(new PublicUserDto(user.Id, user.Username, user.Email));
        }
        
        [HttpPost("verification")]
        public async Task<IActionResult> VerifyUser(User user)
        {
            var token = await emailVerificationService.InitiateVerification(user, EmailVerificationReason.AccountEditing);
            Response.Cookies.Append(VerificationCookieName, token, new CookieOptions()
            {
                Expires = DateTimeOffset.UtcNow.AddSeconds(emailCodeOptions.Value.TtlSeconds),
                HttpOnly = true,
                MaxAge = TimeSpan.FromSeconds(emailCodeOptions.Value.TtlSeconds),
                Secure = true,
                SameSite = SameSiteMode.Strict
            });
            return Ok();
        }

        // [HttpPost("password")]
        // public async Task<IActionResult> ChangePassword(User user,
        //     [MaxLength(64)]
        //     [MinLength(8)]
        //     [FromBody] string password,
        //     string code
        //     )
        // {
        //     var token = Request.Cookies[VerificationCookieName] ?? throw new UnauthorizedException("No verification token");
        //     var verifiedUser = await emailVerificationService.PeekVerification(code, token, EmailVerificationReason.AccountEditing);
        //     if (verifiedUser is not null && verifiedUser.Id == user.Id)
        //     {
        //         var userFromRepo = await appDbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        //         
        //     }
        // }
        
    }
}
