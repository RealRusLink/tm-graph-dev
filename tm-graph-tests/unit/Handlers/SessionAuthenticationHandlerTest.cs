using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tm_graph_src;
using tm_graph_src.Exceptions;
using tm_graph_src.Options;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class SessionAuthenticationHandlerTest : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly IHashService _hash = new HashService();
    private readonly SessionService _sessionService;
    private readonly DefaultHttpContext _httpContext = new();
    private readonly SessionAuthenticationHandler _handler;

    public SessionAuthenticationHandlerTest()
    {
        _db = _database.CreateContext();
        _sessionService = new SessionService(_db, _hash);
        _handler = new SessionAuthenticationHandler(
            _sessionService,
            new StubOptionsMonitor<SessionAuthenticationOptions>(new SessionAuthenticationOptions()),
            Options.Create(new SessionLifetimeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
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

    private async Task<User> AddUserAsync(string userId = "user-42")
    {
        var user = new User
        {
            Id = userId,
            Username = $"user-{userId}",
            Email = $"{userId}@example.com",
            Hash = "hash"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<(User User, string Token)> CreateSessionAsync(string userAgent = "test-agent")
    {
        var user = await AddUserAsync();
        var (token, _) = await _sessionService.CreateSession(user.Id, userAgent, TimeSpan.FromDays(1));

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
    public async Task Authenticate_ShouldFail_AndDeleteExpiredSession()
    {
        var (_, token) = await CreateSessionAsync();
        var session = await _db.Sessions.SingleAsync();
        session.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _db.SaveChangesAsync();
        await InitializeAsync();
        SetSessionCookie(token);
        _httpContext.Request.Headers.UserAgent = "test-agent";

        var result = await _handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Session expired", result.Failure!.Message);
        Assert.False(await _db.Sessions.AnyAsync());
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

        var authenticatedUser = Assert.IsType<User>(
            _httpContext.Items[SessionAuthenticationOptions.ContextItemUserKey]);
        Assert.Equal(user.Id, authenticatedUser.Id);
        Assert.Equal(user.Username, authenticatedUser.Username);
        Assert.Equal(user.Email, authenticatedUser.Email);
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
        await AddUserAsync("user-42");
        await InitializeAsync();
        _httpContext.Request.Headers.UserAgent = "test-agent";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-42")
        ]));

        await _handler.SignInAsync(principal, properties: null);

        var session = await _db.Sessions.SingleAsync();
        Assert.Equal("user-42", session.UserId);
        Assert.Equal("test-agent", session.UserAgent);

        var cookie = SetCookieHeader(_httpContext)!;
        var token = ExtractToken(cookie);
        Assert.Equal(_hash.Hash(token), session.TokenHash);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignIn_ShouldUseShortLifetime_WhenNotPersistent()
    {
        await AddUserAsync("user-42");
        await InitializeAsync();
        _httpContext.Request.Headers.UserAgent = "test-agent";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-42")
        ]));
        var before = DateTimeOffset.UtcNow;

        await _handler.SignInAsync(principal, properties: null);

        var session = await _db.Sessions.SingleAsync();
        Assert.InRange(session.ExpiresAt, before.AddHours(2), DateTimeOffset.UtcNow.AddHours(2));
    }

    [Fact]
    public async Task SignIn_ShouldUseLongLifetime_WhenPersistent()
    {
        await AddUserAsync("user-42");
        await InitializeAsync();
        _httpContext.Request.Headers.UserAgent = "test-agent";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-42")
        ]));
        var before = DateTimeOffset.UtcNow;

        await _handler.SignInAsync(principal, new AuthenticationProperties { IsPersistent = true });

        var session = await _db.Sessions.SingleAsync();
        Assert.InRange(session.ExpiresAt, before.AddDays(30), DateTimeOffset.UtcNow.AddDays(30));
    }

    [Fact]
    public async Task SignIn_ShouldThrow_WithoutUserIdClaim()
    {
        await InitializeAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        await Assert.ThrowsAsync<InternalServerErrorException>(
            () => _handler.SignInAsync(principal, properties: null));

        Assert.False(await _db.Sessions.AnyAsync());
        Assert.Null(SetCookieHeader(_httpContext));
    }

    [Fact]
    public async Task SignOut_ShouldRevokeSessionAndDeleteCookie()
    {
        var (_, token) = await CreateSessionAsync();
        await InitializeAsync();
        SetSessionCookie(token);

        await _handler.SignOutAsync(properties: null);

        Assert.False(await _db.Sessions.AnyAsync());
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookieHeader(_httpContext)!);
    }
}
