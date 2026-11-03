using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tm_graph_src;
using tm_graph_src.Controllers;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class AuthEndpointsTest : IDisposable
{
    private const string Password = "password123";

    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthEndpointsTest()
    {
        _client = CreateClient(_factory);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static HttpClient CreateClient(AuthApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private Task<HttpResponseMessage> RegisterAsync(
        string username = "newuser",
        string email = "new@example.com",
        string password = Password) =>
        _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, email, password));

    private Task<HttpResponseMessage> ConfirmAsync(string code, bool rememberMe = false) =>
        _client.PostAsJsonAsync("/api/auth/confirm", new ConfirmRequest(code, rememberMe));

    private Task<HttpResponseMessage> LoginAsync(
        string identifier,
        string password = Password,
        bool rememberMe = false) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(identifier, password, rememberMe));

    private async Task<PublicUserDto> RegisterAndConfirmAsync(
        string username = "newuser",
        string email = "new@example.com",
        bool rememberMe = false)
    {
        var register = await RegisterAsync(username, email);
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        var confirm = await ConfirmAsync(TestServices.ExtractCode(_factory.Email), rememberMe);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        return await ReadUserAsync(confirm);
    }

    private static async Task<PublicUserDto> ReadUserAsync(HttpResponseMessage response)
    {
        var user = await response.Content.ReadFromJsonAsync<PublicUserDto>();
        Assert.NotNull(user);
        return user;
    }

    private IServiceScope CreateScope() => _factory.Services.CreateScope();

    [Fact]
    public async Task Register_ShouldReturnAcceptedAndSendCode()
    {
        var response = await RegisterAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var message = Assert.Single(_factory.Email.Messages);
        Assert.Equal("new@example.com", message.To);
        Assert.Contains("Your code is", message.Text);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{AuthController.RegisterCookieName}=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth/confirm", cookie, StringComparison.OrdinalIgnoreCase);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync());
    }

    [Fact]
    public async Task Register_ShouldNormalizeEmail()
    {
        await RegisterAsync(email: "New@Example.COM");

        var message = Assert.Single(_factory.Email.Messages);
        Assert.Equal("new@example.com", message.To);

        await ConfirmAsync(TestServices.ExtractCode(_factory.Email));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Users.SingleAsync();
        Assert.Equal("new@example.com", stored.Email);
    }

    [Fact]
    public async Task Confirm_ShouldCreateUserAndSession()
    {
        var response = await RegisterAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var code = TestServices.ExtractCode(_factory.Email);
        var confirm = await ConfirmAsync(code);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var dto = await ReadUserAsync(confirm);
        Assert.NotEqual(string.Empty, dto.Id);
        Assert.Equal("newuser", dto.Username);
        Assert.Equal("new@example.com", dto.Email);

        var cookie = Assert.Single(confirm.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{SessionAuthenticationHandler.SessionCookieName}=", cookie);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync();
        var session = await db.Sessions.SingleAsync();

        Assert.Equal(dto.Id, user.Id);
        Assert.Equal(user.Id, session.UserId);
        Assert.True(new PasswordHashService().Verify(user.Hash, Password));
        Assert.InRange(session.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(119),
            DateTimeOffset.UtcNow.AddMinutes(121));
    }

    [Fact]
    public async Task Confirm_ShouldRejectWrongCode_AndKeepUserUncreated()
    {
        await RegisterAsync();

        var response = await ConfirmAsync("000000000000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync());
        Assert.False(await db.Sessions.AnyAsync());
    }

    [Fact]
    public async Task Confirm_ShouldRejectRequestWithoutRegisterCookie()
    {
        using var client = CreateClient(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm",
            new ConfirmRequest("123456789012", false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldRejectDuplicateEmail()
    {
        await RegisterAsync(username: "first", email: "new@example.com");

        var response = await RegisterAsync(username: "second", email: "new@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(_factory.Email.Messages);
    }

    [Fact]
    public async Task Register_ShouldRejectDuplicateUsername()
    {
        await RegisterAsync(username: "newuser", email: "first@example.com");

        var response = await RegisterAsync(username: "newuser", email: "second@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(_factory.Email.Messages);
    }

    [Fact]
    public async Task Login_ShouldAcceptEmail()
    {
        var registered = await RegisterAndConfirmAsync();
        using var client = CreateClient(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("new@example.com", Password, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await ReadUserAsync(response);
        Assert.Equal(registered.Id, user.Id);
        Assert.Equal(registered.Username, user.Username);
        Assert.Equal(registered.Email, user.Email);
    }

    [Fact]
    public async Task Login_ShouldAcceptUsername()
    {
        var registered = await RegisterAndConfirmAsync();
        using var client = CreateClient(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("newuser", Password, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await ReadUserAsync(response);
        Assert.Equal(registered.Id, user.Id);
        Assert.Equal(registered.Username, user.Username);
        Assert.Equal(registered.Email, user.Email);
    }

    [Fact]
    public async Task Login_ShouldRejectWrongPassword()
    {
        await RegisterAndConfirmAsync();
        using var client = CreateClient(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("new@example.com", "wrong-password", false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Wrong identifier or password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_ShouldRejectUnknownIdentifier()
    {
        var response = await LoginAsync("missing@example.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Wrong identifier or password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Logout_ShouldRevokeSession()
    {
        await RegisterAndConfirmAsync();

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.Sessions.CountAsync());
        }

        var response = await _client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await db.Sessions.CountAsync());
        }
    }

    [Fact]
    public async Task Logout_WithoutSession_ShouldReturnNoContent()
    {
        using var client = CreateClient(_factory);

        var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_ShouldUseLongSessionLifetime_WhenRememberMeIsTrue()
    {
        await RegisterAndConfirmAsync(rememberMe: true);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.Sessions.SingleAsync();

        Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task Confirm_ShouldUseShortSessionLifetime_WhenRememberMeIsFalse()
    {
        await RegisterAndConfirmAsync(rememberMe: false);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.Sessions.SingleAsync();

        Assert.True(session.ExpiresAt < DateTimeOffset.UtcNow.AddDays(1));
        Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow.AddHours(1));
    }

    private sealed record RegisterRequest(string Username, string Email, string Password);

    private sealed record ConfirmRequest(
        string Code,
        [property: JsonPropertyName("remember_me")]
        bool RememberMe);

    private sealed record LoginRequest(
        string Identifier,
        string Password,
        [property: JsonPropertyName("remember_me")]
        bool RememberMe);
}
