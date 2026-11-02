using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using tm_graph.Types;
using tm_graph_src;
using tm_graph_src.Controllers;
using tm_graph_src.Exceptions;
using tm_graph_src.Options;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class AuthControllerTest
{
    private const string CookieName = AuthController.RegisterCookieName;

    private readonly FakeUserStore _users = new();
    private readonly FakeEmailService _email;
    private readonly EmailVerificationService _verification;
    private readonly PasswordHashService _passwords = new();
    private readonly FakeAuthenticationService _auth;
    private readonly AuthController _controller;

    public AuthControllerTest()
    {
        (_verification, _email) = TestServices.CreateEmailVerification();
        var (httpContext, auth) = TestServices.CreateHttpContext();
        _auth = auth;
        _controller = new AuthController(
            _users,
            _verification,
            new UniquenessVerifierService(_users, _verification),
            _passwords,
            Options.Create(new EmailVerificationCodeOptions { TtlSeconds = 60 }))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static CreateUserDto Dto(string username = "newuser", string email = "new@example.com") =>
        new(username, email, "password123");

    private static User PendingUser(string username = "newuser", string email = "new@example.com") => new()
    {
        Username = username,
        Email = email,
        Hash = "bcrypt-hash"
    };

    private void SetSessionCookie(string session) =>
        _controller.Request.Headers.Cookie = $"{CookieName}={session}";

    private string SessionFromResponse()
    {
        var cookie = Assert.Single(_controller.Response.Headers.SetCookie)!;
        var pair = cookie.Split(';')[0];
        return pair[(CookieName.Length + 1)..];
    }

    private async Task<User> RegisterAndConfirmAsync(
        string username = "newuser",
        string email = "new@example.com",
        bool rememberMe = false)
    {
        await _controller.Register(Dto(username, email));
        var code = TestServices.ExtractCode(_email);
        SetSessionCookie(SessionFromResponse());

        await _controller.Confirm(new ConfirmDto(code, rememberMe));
        return Assert.Single(_users.Users);
    }

    [Fact]
    public async Task Register_ShouldSendVerificationEmailAndSetCookie()
    {
        var result = await _controller.Register(Dto());

        Assert.IsType<AcceptedResult>(result);

        var message = Assert.Single(_email.Messages);
        Assert.Equal("new@example.com", message.To);

        var cookie = Assert.Single(_controller.Response.Headers.SetCookie);
        Assert.Contains($"{CookieName}=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth/confirm", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ShouldNotPersistUserBeforeConfirmation()
    {
        await _controller.Register(Dto());

        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Register_ShouldThrowConflict_WhenEmailIsTaken()
    {
        _users.Users.Add(new User { Username = "someone", Email = "new@example.com", Hash = "hash" });

        await Assert.ThrowsAsync<ConflictException>(() => _controller.Register(Dto()));

        Assert.Empty(_email.Messages);
    }

    [Fact]
    public async Task Register_ShouldNormalizeEmail()
    {
        await _controller.Register(Dto(email: " New@Example.COM "));
        var code = TestServices.ExtractCode(_email);
        SetSessionCookie(SessionFromResponse());

        await _controller.Confirm(new ConfirmDto(code));

        var stored = Assert.Single(_users.Users);
        Assert.Equal("new@example.com", stored.Email);
        Assert.Equal("new@example.com", _email.Messages[0].To);
    }

    [Fact]
    public async Task Confirm_ShouldPersistUserWithHashedPassword()
    {
        await _controller.Register(Dto());
        var code = TestServices.ExtractCode(_email);
        SetSessionCookie(SessionFromResponse());

        var result = await _controller.Confirm(new ConfirmDto(code));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<PublicUserDto>(ok.Value);
        var stored = Assert.Single(_users.Users);

        Assert.Equal(stored.Id, dto.Id);
        Assert.Equal("newuser", dto.Username);
        Assert.Equal("new@example.com", dto.Email);
        Assert.Equal("newuser", stored.Username);
        Assert.Equal("new@example.com", stored.Email);
        Assert.NotEqual("password123", stored.Hash);
        Assert.True(_passwords.Verify(stored.Hash, "password123"));
        Assert.Equal(1, _users.AddCalls);

        var signIn = Assert.Single(_auth.SignIns);
        Assert.Equal(SessionAuthenticationOptions.SchemeName, signIn.Scheme);
        Assert.False(signIn.Properties!.IsPersistent);
    }

    [Fact]
    public async Task Confirm_ShouldPassRememberMe()
    {
        await _controller.Register(Dto());
        var code = TestServices.ExtractCode(_email);
        SetSessionCookie(SessionFromResponse());

        await _controller.Confirm(new ConfirmDto(code, RememberMe: true));

        var signIn = Assert.Single(_auth.SignIns);
        Assert.True(signIn.Properties!.IsPersistent);
    }

    [Fact]
    public async Task Confirm_ShouldThrowBadRequest_ForWrongCode()
    {
        var session = await _verification.InitiateVerification(PendingUser(), EmailVerificationReason.Registration);
        SetSessionCookie(session);

        await Assert.ThrowsAsync<BadRequestException>(
            () => _controller.Confirm(new ConfirmDto("000000000000")));

        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Confirm_ShouldThrowBadRequest_WithoutSessionCookie()
    {
        await _verification.InitiateVerification(PendingUser(), EmailVerificationReason.Registration);

        await Assert.ThrowsAsync<BadRequestException>(
            () => _controller.Confirm(new ConfirmDto(TestServices.ExtractCode(_email))));

        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Confirm_ShouldNotBeReusable()
    {
        var session = await _verification.InitiateVerification(PendingUser(), EmailVerificationReason.Registration);
        SetSessionCookie(session);
        var code = TestServices.ExtractCode(_email);

        await _controller.Confirm(new ConfirmDto(code));

        await Assert.ThrowsAsync<BadRequestException>(
            () => _controller.Confirm(new ConfirmDto(code)));
        Assert.Single(_users.Users);
        Assert.Single(_auth.SignIns);
    }

    [Fact]
    public async Task Login_ShouldReturnBadRequest_ForUnknownEmail()
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _controller.Login(new LoginDto("missing@example.com", "password123")));

        Assert.Equal("Wrong identifier or password", exception.Details);
        Assert.Empty(_auth.SignIns);
    }

    [Fact]
    public async Task Login_ShouldReturnBadRequest_ForWrongPassword()
    {
        await RegisterAndConfirmAsync();
        _auth.SignIns.Clear();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _controller.Login(new LoginDto("new@example.com", "wrong-password")));

        Assert.Equal("Wrong identifier or password", exception.Details);
        Assert.Empty(_auth.SignIns);
    }

    [Fact]
    public async Task Login_ShouldBeCaseInsensitiveForEmail()
    {
        var stored = await RegisterAndConfirmAsync();
        _auth.SignIns.Clear();

        var result = await _controller.Login(new LoginDto(" NEW@EXAMPLE.COM ", "password123"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<PublicUserDto>(ok.Value);
        Assert.Equal(stored.Id, dto.Id);
        Assert.Equal("newuser", dto.Username);
        Assert.Equal("new@example.com", dto.Email);
    }

    [Fact]
    public async Task Login_ShouldBeCaseSensitiveForUsername()
    {
        await RegisterAndConfirmAsync(username: "newuser");
        _auth.SignIns.Clear();

        await Assert.ThrowsAsync<BadRequestException>(
            () => _controller.Login(new LoginDto("NewUser", "password123")));

        Assert.Empty(_auth.SignIns);
    }

    [Fact]
    public async Task Login_ShouldPassRememberMe()
    {
        await RegisterAndConfirmAsync();
        _auth.SignIns.Clear();

        await _controller.Login(new LoginDto("newuser", "password123", RememberMe: true));

        var signIn = Assert.Single(_auth.SignIns);
        Assert.True(signIn.Properties!.IsPersistent);
    }

    [Fact]
    public async Task Logout_ShouldReturnNoContentAndSignOut()
    {
        var result = await _controller.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.Single(_auth.SignOuts);
    }
}
