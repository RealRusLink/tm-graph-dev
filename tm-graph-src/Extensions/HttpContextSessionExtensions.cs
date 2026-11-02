using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace tm_graph_src.Extensions;

public static class HttpContextSessionExtensions
{
    public static async Task UserSignIn(this HttpContext context, User user, bool rememberMe = false)
    {
        var schemeProvider = context.RequestServices
            .GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetDefaultSignInSchemeAsync();
        if (scheme is null) throw new ArgumentNullException();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id)
        };
        var identity = new ClaimsIdentity(
            claims, scheme.Name
        );
        var claimsPrincipal = new ClaimsPrincipal(identity);
        await context.SignInAsync(scheme.Name, claimsPrincipal, new AuthenticationProperties
        {
            IsPersistent = rememberMe
        });
    }

    public static async Task UserSignOut(this HttpContext context)
    {
        await context.SignOutAsync();
    }
    
}