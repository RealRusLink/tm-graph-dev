using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace tm_graph_src.Services;

public class SessionHandler : AuthenticationHandler<SessionAuthenticationOptions>, IAuthenticationSignInHandler, IAuthenticationSignOutHandler
{
    public SessionHandler(
        AppDbContext appDbContext,
        IOptionsMonitor<SessionAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {}
    
    
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        throw new NotImplementedException();
    }

    public async Task SignOutAsync(AuthenticationProperties? properties)
    {
        throw new NotImplementedException();
    }

    public async Task SignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        throw new NotImplementedException();
    }
}