using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using tm_graph_src.Exceptions;
using tm_graph_src.Options;
    
namespace tm_graph_src.Services;

public class SessionAuthenticationHandler(
    SessionService sessionService,
    IOptionsMonitor<SessionAuthenticationOptions> options,
    IOptions<SessionLifetimeOptions> sessionOptions,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<SessionAuthenticationOptions>(options, logger, encoder), IAuthenticationSignInHandler,
        IAuthenticationSignOutHandler
{

    public const string SessionCookieName = "X-Session-Token";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sessionToken = Request.Cookies[SessionCookieName];
        if (sessionToken is null) return AuthenticateResult.NoResult();
        var session = await sessionService.GetSession(sessionToken);
        if (session is null) 
        {
            Response.Cookies.Delete(SessionCookieName);
            return AuthenticateResult.Fail("Bad session token");
        }
        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await sessionService.RevokeSession(sessionToken);
            Response.Cookies.Delete(SessionCookieName);
            return AuthenticateResult.Fail("Session expired");
        }
        if (session.UserAgent != Request.Headers.UserAgent.ToString())
        {
            Response.Cookies.Delete(SessionCookieName);
            return AuthenticateResult.Fail("User agent changed");
        }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User.Id)
        };
        var identity = new ClaimsIdentity(
            claims, Scheme.Name
        );
        var claimsPrincipal = new ClaimsPrincipal(identity);
        Context.Items[SessionAuthenticationOptions.ContextItemUserKey] = session.User;
        return AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name));
    }

    public async Task SignOutAsync(AuthenticationProperties? properties)
    {
        var token = Request.Cookies[SessionCookieName];
        if (token is not null)
        {
            await sessionService.RevokeSession(token);
        }
        Response.Cookies.Delete(SessionCookieName);
    }

    public async Task SignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) throw new InternalServerErrorException("Can't sign user in without user Id.");
        var lifetime = properties?.IsPersistent == true
            ? sessionOptions.Value.LongLifetime
            : sessionOptions.Value.ShortLifetime;
        var (token, expiresAt) = await sessionService.CreateSession(
            userId,
            Request.Headers.UserAgent.ToString(),
            lifetime);
        Response.Cookies.Append(SessionCookieName, token, new CookieOptions()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            MaxAge = lifetime
        });
    }
}
