using tm_graph_src.Services;

namespace tm_graph_tests;

public class PasswordHashServiceTest
{
    private readonly PasswordHashService _service = new();

    [Fact]
    public void Hash_ShouldNotReturnPlaintext()
    {
        const string password = "correct horse battery staple";

        var hash = _service.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void Hash_ShouldProduceVerifiableHash()
    {
        const string password = "P@ssw0rd!";

        var hash = _service.Hash(password);

        Assert.True(_service.Verify(hash, password));
    }

    [Fact]
    public void Hash_ShouldUseSalt_SoSamePasswordYieldsDifferentHashes()
    {
        const string password = "same-password";

        var first = _service.Hash(password);
        var second = _service.Hash(password);

        Assert.NotEqual(first, second);
        Assert.True(_service.Verify(first, password));
        Assert.True(_service.Verify(second, password));
    }

    [Theory]
    [InlineData("password", "Password")]
    [InlineData("password", "password ")]
    [InlineData("password", "")]
    [InlineData("специальные символы 🚀", "специальные символы")]
    public void Verify_ShouldReturnFalse_ForWrongPassword(string password, string provided)
    {
        var hash = _service.Hash(password);

        Assert.False(_service.Verify(hash, provided));
    }

    [Fact]
    public void Verify_ShouldBeCaseSensitive()
    {
        var hash = _service.Hash("Secret");

        Assert.True(_service.Verify(hash, "Secret"));
        Assert.False(_service.Verify(hash, "secret"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("специальные символы 🚀")]
    [InlineData("a")]
    public void Hash_ShouldRoundTrip_ForEdgeCasePasswords(string password)
    {
        var hash = _service.Hash(password);

        Assert.True(_service.Verify(hash, password));
        Assert.False(_service.Verify(hash, password + "x"));
    }
}
