using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tm_graph_src;
using tm_graph_src.Exceptions;
using tm_graph_src.Options;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class SessionAuthenticationHandlerTest
{
    private readonly FakeSessionStore _store = new();
    private readonly SessionService _sessionService;
    private readonly DefaultHttpContext _httpContext = new();
    private readonly SessionAuthenticationHandler _handler;

    public SessionAuthenticationHandlerTest()
    {
        _sessionService = new SessionService(_store, new HashService());
        _handler = new SessionAuthenticationHandler(
            _sessionService,
            new StubOptionsMonitor<SessionAuthenticationOptions>(new SessionAuthenticationOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);
    }

    private sealed class StubOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private async Task<SessionAuthenticationHandlerTest> InitializeAsync()
    {
        await _handler.InitializeAsync(
            new AuthenticationScheme(
                SessionAuthenticationOptions.SchemeName,
                displayName: null,
                handlerType: typeof(SessionAuthenticationHandler)),
            _httpContext);

        return this;
    }

    private async Task<(User User, string Token)> CreateSessionAsync(string userAgent = "test-agent")
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "user",
            Email = "user@example.com",
            Hash = "hash"
        };
        var token = await _sessionService.CreateSession(user.Id, userAgent);

        // SessionStore подгружает User через Include, фейк делаем таким же
        _store.Sessions[^1].User = user;
        return (user, token);
    }

    private void SetSessionCookie(string token) =>
        _httpContext.Request.Headers.Cookie = $"{SessionAuthenticationHandler.SessionCookieName}={token}";

    private static string? SetCookieHeader(DefaultHttpContext context) =>
        context.Response.Headers.SetCookie.FirstOrDefault();

    private static string ExtractToken(string setCookie) => setCookie.Split(';')[0].Split('=')[1];

    [Fact]
    public async Task Authenticate_ShouldReturnNoResult_WithoutCookie()
    {
        await InitializeAsync();

        var result = await _handler.AuthenticateAsync();

        Assert.True(result.None);
        Assert.False(result.Succeeded);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task Authenticate_ShouldFail_AndDeleteCookie_ForUnknownSession()
    {
        await InitializeAsync();
        SetSessionCookie("unknown-token");

        var result = await _handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Bad session token", result.Failure!.Message);

        var cookie = SetCookieHeader(_httpContext);
        Assert.NotNull(cookie);
        Assert.Contains(SessionAuthenticationHandler.SessionCookieName, cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie);
    }

    [Fact]
    public async Task Authenticate_ShouldFail_WhenUserAgentChanged()
    {
        var (_, token) = await CreateSessionAsync("original-agent");
        await InitializeAsync();
        SetSessionCookie(token);
        _httpContext.Request.Headers.UserAgent = "another-agent";

        var result = await _handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("User agent changed", result.Failure!.Message);
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookieHeader(_httpContext)!);
    }

    [Fact]
    public async Task Authenticate_ShouldSucceed_AndExposeUserAndClaim()
    {
        var (user, token) = await CreateSessionAsync("test-agent");
        await InitializeAsync();
        SetSessionCookie(token);
        _httpContext.Request.Headers.UserAgent = "test-agent";

        var result = await _handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(user.Id, result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(SessionAuthenticationOptions.SchemeName, result.Principal.Identity!.AuthenticationType);
        Assert.Same(user, _httpContext.Items[SessionAuthenticationOptions.ContextItemUserKey]);
    }

    [Fact]
    public async Task Authenticate_ShouldNotDeleteCookie_OnSuccess()
    {
        var (_, token) = await CreateSessionAsync();
        await InitializeAsync();
        SetSessionCookie(token);
        _httpContext.Request.Headers.UserAgent = "test-agent";

        await _handler.AuthenticateAsync();

        Assert.Null(SetCookieHeader(_httpContext));
    }

    [Fact]
    public async Task SignIn_ShouldCreateSessionAndSetCookie()
    {
        await InitializeAsync();
        _httpContext.Request.Headers.UserAgent = "test-agent";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-42")
        ]));

        await _handler.SignInAsync(principal, properties: null);

        var session = Assert.Single(_store.Sessions);
        Assert.Equal("user-42", session.UserId);
        Assert.Equal("test-agent", session.UserAgent);

        var cookie = SetCookieHeader(_httpContext)!;
        var token = ExtractToken(cookie);
        Assert.Equal(new HashService().Hash(token), session.TokenHash);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignIn_ShouldThrow_WithoutUserIdClaim()
    {
        await InitializeAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        await Assert.ThrowsAsync<InternalServerErrorException>(
            () => _handler.SignInAsync(principal, properties: null));

        Assert.Empty(_store.Sessions);
        Assert.Null(SetCookieHeader(_httpContext));
    }

    [Fact]
    public async Task SignOut_ShouldRevokeSessionAndDeleteCookie()
    {
        var (_, token) = await CreateSessionAsync();
        await InitializeAsync();
        SetSessionCookie(token);

        await _handler.SignOutAsync(properties: null);

        Assert.Empty(_store.Sessions);
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookieHeader(_httpContext)!);
    }
}
