using Microsoft.EntityFrameworkCore;
using tm_graph.Types;
using tm_graph_src;
using tm_graph_src.Controllers;
using tm_graph_src.Exceptions;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class UniquenessVerifierServiceTest : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly EmailVerificationService _verification;
    private readonly UniquenessVerifierService _service;

    public UniquenessVerifierServiceTest()
    {
        _db = _database.CreateContext();
        (_verification, _) = TestServices.CreateEmailVerification();
        _service = new UniquenessVerifierService(_db, _verification);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static CreateUserDto Dto(string username = "newuser", string email = "new@example.com") =>
        new(username, email, "password123");

    private static User User(string username, string email) => new()
    {
        Username = username,
        Email = email,
        Hash = "hash"
    };

    private async Task AddUserAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task UserUnique_ShouldPass_WhenNothingConflicts()
    {
        await _service.UserUnique(Dto(), EmailVerificationReason.Registration);
    }

    [Fact]
    public async Task UserUnique_ShouldThrow_WhenEmailIsRegistered()
    {
        await AddUserAsync(User("someone", "new@example.com"));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.UserUnique(Dto(), EmailVerificationReason.Registration));

        Assert.Equal("User with this email already exists", exception.Details);
    }

    [Fact]
    public async Task UserUnique_ShouldThrow_WhenUsernameIsRegistered()
    {
        await AddUserAsync(User("newuser", "someone@example.com"));

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
    public async Task UserUnique_ShouldNotModifyDatabase()
    {
        await AddUserAsync(User("someone", "someone@example.com"));
        var before = await _db.Users.CountAsync();

        await _service.UserUnique(Dto(), EmailVerificationReason.Registration);

        Assert.Equal(before, await _db.Users.CountAsync());
    }
}
