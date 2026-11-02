using Microsoft.AspNetCore.Authentication;

namespace tm_graph_src.Options;

public class SessionAuthenticationOptions : AuthenticationSchemeOptions
{
    public static string SchemeName = "CookieSession";
    public static string ContextItemUserKey = "UserKey";
}