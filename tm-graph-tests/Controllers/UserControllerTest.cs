using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using tm_graph.Types;
using tm_graph_src;
using tm_graph_src.Controllers;
using tm_graph_src.Exceptions;
using tm_graph_src.Options;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class UserControllerTest
{
    private const string CookieName = "X-Register-Verification-Token";

    private readonly FakeUserStore _users = new();
    private readonly FakeEmailService _email;
    private readonly EmailVerificationService _verification;
    private readonly PasswordHashService _passwords = new();
    private readonly FakeAuthenticationService _auth;
    private readonly UserController _controller;

    public UserControllerTest()
    {
        (_verification, _email) = TestServices.CreateEmailVerification();
        var (httpContext, auth) = TestServices.CreateHttpContext();
        _auth = auth;
        _controller = new UserController(
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

    [Fact]
    public async Task Register_ShouldSendVerificationEmailAndSetCookie()
    {
        var result = await _controller.Register(Dto());

        Assert.IsType<OkResult>(result);

        var message = Assert.Single(_email.Messages);
        Assert.Equal("new@example.com", message.To);

        var cookie = Assert.Single(_controller.Response.Headers.SetCookie);
        Assert.Contains($"{CookieName}=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
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
    public async Task Confirm_ShouldPersistUserWithHashedPassword()
    {
        await _controller.Register(Dto());
        var code = TestServices.ExtractCode(_email);
        SetSessionCookie(SessionFromResponse());

        var result = await _controller.CheckByCode(code);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<PublicUserDto>(ok.Value);
        var stored = Assert.Single(_users.Users);

        Assert.Equal("newuser", dto.Username);
        Assert.Equal("new@example.com", dto.Email);
        Assert.Equal("newuser", stored.Username);
        Assert.Equal("new@example.com", stored.Email);
        Assert.NotEqual("password123", stored.Hash);
        Assert.True(_passwords.Verify(stored.Hash, "password123"));
        Assert.Equal(1, _users.AddCalls);

        var signIn = Assert.Single(_auth.SignIns);
        Assert.Equal(SessionAuthenticationOptions.SchemeName, signIn.Scheme);
    }

    [Fact]
    public async Task Confirm_ShouldThrowBadRequest_ForWrongCode()
    {
        var session = await _verification.InitiateVerification(PendingUser(), EmailVerificationReason.Registration);
        SetSessionCookie(session);

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.CheckByCode("000000000000"));

        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Confirm_ShouldThrowBadRequest_WithoutSessionCookie()
    {
        await _verification.InitiateVerification(PendingUser(), EmailVerificationReason.Registration);

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.CheckByCode(TestServices.ExtractCode(_email)));

        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Confirm_ShouldNotBeReusable()
    {
        var session = await _verification.InitiateVerification(PendingUser(), EmailVerificationReason.Registration);
        SetSessionCookie(session);
        var code = TestServices.ExtractCode(_email);

        await _controller.CheckByCode(code);

        await Assert.ThrowsAsync<BadRequestException>(() => _controller.CheckByCode(code));
        Assert.Single(_users.Users);
        Assert.Single(_auth.SignIns);
    }
}
