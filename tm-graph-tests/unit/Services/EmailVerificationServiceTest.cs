using tm_graph.Types;
using tm_graph_src;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class EmailVerificationServiceTest
{
    private static User NewUser(string suffix = "1") => new()
    {
        Username = $"user{suffix}",
        Email = $"user{suffix}@example.com",
        Hash = "bcrypt-hash"
    };

    private static (EmailVerificationService Service, FakeEmailService Email) CreateService(int ttlSeconds = 60) =>
        TestServices.CreateEmailVerification(ttlSeconds);

    private static string ExtractCode(FakeEmailService email) => TestServices.ExtractCode(email);

    [Fact]
    public async Task InitiateVerification_ShouldSendCodeToUserEmail()
    {
        var (service, email) = CreateService();
        var user = NewUser();

        var session = await service.InitiateVerification(user, EmailVerificationReason.Registration);

        var message = Assert.Single(email.Messages);
        Assert.Equal(user.Email, message.To);
        Assert.Equal("Verification code", message.Subject);
        Assert.Contains("Your code is", message.Text);
        Assert.False(string.IsNullOrWhiteSpace(session));
    }

    [Fact]
    public async Task InitiateVerification_ShouldGenerateExpectedFormatOfSessionAndCode()
    {
        var (service, email) = CreateService();

        var session = await service.InitiateVerification(NewUser(), EmailVerificationReason.Registration);
        var code = ExtractCode(email);

        Assert.Equal(CryptoConsts.TokenLength, session.Length);
        Assert.All(session, c => Assert.Contains(c, CryptoConsts.Alphanumeric));
        Assert.Equal(CryptoConsts.CodeLength, code.Length);
        Assert.All(code, c => Assert.Contains(c, CryptoConsts.Digits));
    }

    [Fact]
    public async Task InitiateVerification_ShouldReturnDifferentSessionAndCode_ForEachCall()
    {
        var (service, _) = CreateService();

        var first = await service.InitiateVerification(NewUser("1"), EmailVerificationReason.Registration);
        var second = await service.InitiateVerification(NewUser("2"), EmailVerificationReason.Registration);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task CompleteVerification_ShouldReturnUser_ForValidCodeAndSession()
    {
        var (service, email) = CreateService();
        var user = NewUser();
        var session = await service.InitiateVerification(user, EmailVerificationReason.Registration);

        var verified = await service.CompleteVerification(ExtractCode(email), session, EmailVerificationReason.Registration);

        Assert.NotNull(verified);
        Assert.Equal(user.Id, verified!.Id);
        Assert.Equal(user.Email, verified.Email);
        Assert.Equal(user.Username, verified.Username);
        Assert.Equal(user.Hash, verified.Hash);
    }

    [Theory]
    [InlineData("000000000000")]
    [InlineData("")]
    [InlineData("not-a-code")]
    public async Task CompleteVerification_ShouldReturnNull_ForWrongCode(string wrongCode)
    {
        var (service, email) = CreateService();
        var session = await service.InitiateVerification(NewUser(), EmailVerificationReason.Registration);
        var code = ExtractCode(email);

        if (wrongCode == code) return; // крайне маловероятно, но не должно ломать тест

        var verified = await service.CompleteVerification(wrongCode, session, EmailVerificationReason.Registration);

        Assert.Null(verified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-session")]
    public async Task CompleteVerification_ShouldReturnNull_ForWrongSession(string wrongSession)
    {
        var (service, email) = CreateService();
        await service.InitiateVerification(NewUser(), EmailVerificationReason.Registration);

        var verified = await service.CompleteVerification(ExtractCode(email), wrongSession, EmailVerificationReason.Registration);

        Assert.Null(verified);
    }

    [Fact]
    public async Task CompleteVerification_ShouldReturnNull_WhenReasonDiffers()
    {
        var (service, email) = CreateService();
        var session = await service.InitiateVerification(NewUser(), EmailVerificationReason.Registration);
        var code = ExtractCode(email);

        var verified = await service.CompleteVerification(code, session, EmailVerificationReason.PasswordReset);

        Assert.Null(verified);
        Assert.NotNull(await service.CompleteVerification(code, session, EmailVerificationReason.Registration));
    }

    [Fact]
    public async Task CompleteVerification_ShouldBeSingleUse()
    {
        var (service, email) = CreateService();
        var session = await service.InitiateVerification(NewUser(), EmailVerificationReason.Registration);
        var code = ExtractCode(email);

        Assert.NotNull(await service.CompleteVerification(code, session, EmailVerificationReason.Registration));
        Assert.Null(await service.CompleteVerification(code, session, EmailVerificationReason.Registration));
    }

    [Fact]
    public async Task InitiateVerification_ShouldMarkEmailAndUsernamePending()
    {
        var (service, _) = CreateService();
        var user = NewUser();

        Assert.False(await service.EmailPending(user.Email, EmailVerificationReason.Registration));
        Assert.False(await service.UsernamePending(user.Username, EmailVerificationReason.Registration));

        await service.InitiateVerification(user, EmailVerificationReason.Registration);

        Assert.True(await service.EmailPending(user.Email, EmailVerificationReason.Registration));
        Assert.True(await service.UsernamePending(user.Username, EmailVerificationReason.Registration));
        Assert.True(await service.IsVerificationPending(user, EmailVerificationReason.Registration));
    }

    [Fact]
    public async Task CompleteVerification_ShouldClearPendingState()
    {
        var (service, email) = CreateService();
        var user = NewUser();
        var session = await service.InitiateVerification(user, EmailVerificationReason.Registration);
        var code = ExtractCode(email);

        await service.CompleteVerification(code, session, EmailVerificationReason.Registration);

        Assert.False(await service.EmailPending(user.Email, EmailVerificationReason.Registration));
        Assert.False(await service.UsernamePending(user.Username, EmailVerificationReason.Registration));
        Assert.False(await service.IsVerificationPending(user, EmailVerificationReason.Registration));
    }

    [Fact]
    public async Task InitiateVerification_ShouldThrow_WhenEmailIsAlreadyPending()
    {
        var (service, _) = CreateService();
        var user = NewUser();
        await service.InitiateVerification(user, EmailVerificationReason.Registration);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.InitiateVerification(user, EmailVerificationReason.Registration));
    }

    [Fact]
    public async Task InitiateVerification_ShouldThrow_WhenUsernameIsAlreadyPending_EvenForAnotherUser()
    {
        var (service, _) = CreateService();
        var first = NewUser();
        var sameUsername = new User
        {
            Username = first.Username,
            Email = "another@example.com",
            Hash = "hash"
        };

        await service.InitiateVerification(first, EmailVerificationReason.Registration);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.InitiateVerification(sameUsername, EmailVerificationReason.Registration));
    }

    [Fact]
    public async Task InitiateVerification_ShouldAllowSameUser_ForDifferentReasons()
    {
        var (service, _) = CreateService();
        var user = NewUser();

        await service.InitiateVerification(user, EmailVerificationReason.Registration);
        var session = await service.InitiateVerification(user, EmailVerificationReason.PasswordReset);

        Assert.False(string.IsNullOrWhiteSpace(session));
        Assert.True(await service.IsVerificationPending(user, EmailVerificationReason.Registration));
        Assert.True(await service.IsVerificationPending(user, EmailVerificationReason.PasswordReset));
    }

    [Fact]
    public async Task InitiateVerification_ShouldRollbackPendingState_WhenEmailSendingFails()
    {
        var (service, email) = CreateService();
        var user = NewUser();
        email.FailWith = new InvalidOperationException("SMTP is down");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InitiateVerification(user, EmailVerificationReason.Registration));

        Assert.Empty(email.Messages);
        Assert.False(await service.IsVerificationPending(user, EmailVerificationReason.Registration));

        // после сбоя повторная попытка должна проходить
        email.FailWith = null;
        var session = await service.InitiateVerification(user, EmailVerificationReason.Registration);

        Assert.Single(email.Messages);
        Assert.NotNull(await service.CompleteVerification(ExtractCode(email), session, EmailVerificationReason.Registration));
    }

    [Fact]
    public async Task VerificationState_ShouldExpire_ByTtl()
    {
        var (service, email) = CreateService(ttlSeconds: 1);
        var user = NewUser();
        var session = await service.InitiateVerification(user, EmailVerificationReason.Registration);
        var code = ExtractCode(email);

        Assert.True(await service.IsVerificationPending(user, EmailVerificationReason.Registration));

        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        Assert.False(await service.IsVerificationPending(user, EmailVerificationReason.Registration));
        Assert.Null(await service.CompleteVerification(code, session, EmailVerificationReason.Registration));
    }

    [Fact]
    public async Task InitiateVerification_ShouldAllowOnlyOnePendingRequest_UnderConcurrency()
    {
        var (service, email) = CreateService();
        var user = NewUser();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try
            {
                await service.InitiateVerification(user, EmailVerificationReason.Registration);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(success => success));
        Assert.Single(email.Messages);
    }
}
