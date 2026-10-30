using tm_graph.Types;
using tm_graph_src;
using tm_graph_src.Controllers;
using tm_graph_src.Exceptions;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class UniquenessVerifierServiceTest
{
    private readonly FakeUserStore _users = new();
    private readonly EmailVerificationService _verification;
    private readonly UniquenessVerifierService _service;

    public UniquenessVerifierServiceTest()
    {
        (_verification, _) = TestServices.CreateEmailVerification();
        _service = new UniquenessVerifierService(_users, _verification);
    }

    private static CreateUserDto Dto(string username = "newuser", string email = "new@example.com") =>
        new(username, email, "password123");

    private static User User(string username, string email) => new()
    {
        Username = username,
        Email = email,
        Hash = "hash"
    };

    [Fact]
    public async Task UserUnique_ShouldPass_WhenNothingConflicts()
    {
        await _service.UserUnique(Dto(), EmailVerificationReason.Registration);
    }

    [Fact]
    public async Task UserUnique_ShouldThrow_WhenEmailIsRegistered()
    {
        _users.Users.Add(User("someone", "new@example.com"));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.UserUnique(Dto(), EmailVerificationReason.Registration));

        Assert.Equal("User with this email already exists", exception.Details);
    }

    [Fact]
    public async Task UserUnique_ShouldThrow_WhenUsernameIsRegistered()
    {
        _users.Users.Add(User("newuser", "someone@example.com"));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.UserUnique(Dto(), EmailVerificationReason.Registration));

        Assert.Equal("User with this username already exists", exception.Details);
    }

    [Fact]
    public async Task UserUnique_ShouldThrow_WhenEmailIsPending()
    {
        await _verification.InitiateVerification(
            User("otheruser", "new@example.com"),
            EmailVerificationReason.Registration);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.UserUnique(Dto(), EmailVerificationReason.Registration));

        Assert.Equal("User with this email is already pending", exception.Details);
    }

    [Fact]
    public async Task UserUnique_ShouldThrow_WhenUsernameIsPending()
    {
        await _verification.InitiateVerification(
            User("newuser", "other@example.com"),
            EmailVerificationReason.Registration);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.UserUnique(Dto(), EmailVerificationReason.Registration));

        Assert.Equal("User with this username is already pending", exception.Details);
    }

    [Fact]
    public async Task UserUnique_ShouldIgnorePendingOfAnotherReason()
    {
        await _verification.InitiateVerification(
            User("newuser", "new@example.com"),
            EmailVerificationReason.PasswordReset);

        await _service.UserUnique(Dto(), EmailVerificationReason.Registration);
    }

    [Fact]
    public async Task UserUnique_ShouldNotModifyStore()
    {
        _users.Users.Add(User("someone", "someone@example.com"));

        await _service.UserUnique(Dto(), EmailVerificationReason.Registration);

        Assert.Single(_users.Users);
        Assert.Equal(0, _users.AddCalls);
    }
}
