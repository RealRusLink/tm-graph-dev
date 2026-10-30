using tm_graph_src.Services;

namespace tm_graph_tests;

public class HashServiceTest
{
    private readonly IHashService _hasher = new HashService();

    [Fact]
    public void Hash_ShouldReturnConsistentHash_ForSameData()
    {
        var data = new { Name = "Test", Value = 42 };

        var hash1 = _hasher.Hash(data);
        var hash2 = _hasher.Hash(data);

        Assert.Equal(hash1, hash2);
        Assert.False(string.IsNullOrWhiteSpace(hash1));
    }

    [Fact]
    public void Hash_ShouldReturnDifferentHash_ForDifferentData()
    {
        var data1 = new { Name = "Test", Value = 42 };
        var data2 = new { Name = "Test", Value = 43 };

        var hash1 = _hasher.Hash(data1);
        var hash2 = _hasher.Hash(data2);

        Assert.NotEqual(hash1, hash2);
    }

    [Theory]
    [InlineData(123)]
    [InlineData(0)]
    [InlineData(-42)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Hash_ShouldWorkCorrectly_ForVariousIntegers(int value)
    {
        var hash1 = _hasher.Hash(value);
        var hash2 = _hasher.Hash(value);

        Assert.Equal(hash1, hash2);
        Assert.True(_hasher.Verify(value, hash1));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("специальные символы 🚀")]
    public void Hash_ShouldWorkCorrectly_ForStrings(string value)
    {
        var hash = _hasher.Hash(value);

        Assert.True(_hasher.Verify(value, hash));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Hash_ShouldWorkCorrectly_ForBooleans(bool value)
    {
        var hash = _hasher.Hash(value);

        Assert.True(_hasher.Verify(value, hash));
    }

    [Fact]
    public void Verify_ShouldReturnTrue_ForValidHash()
    {
        var data = "secret_data";
        var hash = _hasher.Hash(data);

        var isValid = _hasher.Verify(data, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForInvalidHash()
    {
        var data = "secret_data";
        var wrongHash = _hasher.Hash("other_data");

        var isValid = _hasher.Verify(data, wrongHash);

        Assert.False(isValid);
    }
}