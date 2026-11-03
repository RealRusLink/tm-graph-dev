using Microsoft.Extensions.Caching.Memory;
using tm_graph.Services;
using tm_graph_src.Services;

namespace tm_graph_tests;

public class TypedSetServiceTest
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly ITypedSet<string> _set;

    public TypedSetServiceTest()
    {
        _set = new TypedSetService<string>(new TypedCacheService<string, bool>(_memoryCache));
    }

    private static MemoryCacheEntryOptions Ttl(int milliseconds = 60_000) => new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(milliseconds)
    };

    [Fact]
    public async Task Has_ShouldReturnFalse_WhenValueWasNotAdded()
    {
        Assert.False(await _set.Has("missing"));
    }

    [Fact]
    public async Task Set_ThenHas_ShouldReturnTrue()
    {
        await _set.Set("item", Ttl());

        Assert.True(await _set.Has("item"));
    }

    [Fact]
    public async Task Has_ShouldNotRemoveValue()
    {
        await _set.Set("item", Ttl());

        Assert.True(await _set.Has("item"));
        Assert.True(await _set.Has("item"));
    }

    [Fact]
    public async Task Set_ShouldThrow_WhenValueAlreadyExists()
    {
        await _set.Set("item", Ttl());

        await Assert.ThrowsAsync<ArgumentException>(() => _set.Set("item", Ttl()).AsTask());
    }

    [Fact]
    public async Task Set_ShouldKeepExistingEntry_WhenDuplicateIsRejected()
    {
        await _set.Set("item", Ttl());

        await Assert.ThrowsAsync<ArgumentException>(() => _set.Set("item", Ttl()).AsTask());

        Assert.True(await _set.Has("item"));
    }

    [Fact]
    public async Task Unset_ShouldRemoveValue()
    {
        await _set.Set("item", Ttl());

        await _set.Unset("item");

        Assert.False(await _set.Has("item"));
    }

    [Fact]
    public async Task Unset_ShouldBeIdempotent_ForMissingValue()
    {
        await _set.Unset("never-added");

        Assert.False(await _set.Has("never-added"));
    }

    [Fact]
    public async Task Set_ShouldBeAllowedAgain_AfterUnset()
    {
        await _set.Set("item", Ttl());
        await _set.Unset("item");

        await _set.Set("item", Ttl());

        Assert.True(await _set.Has("item"));
    }

    [Fact]
    public async Task Set_ShouldRespectTtl()
    {
        await _set.Set("item", Ttl(50));

        Assert.True(await _set.Has("item"));

        await Task.Delay(150);

        Assert.False(await _set.Has("item"));
    }

    [Fact]
    public async Task Values_ShouldBeIndependent()
    {
        await _set.Set("first", Ttl());
        await _set.Set("second", Ttl());

        await _set.Unset("first");

        Assert.False(await _set.Has("first"));
        Assert.True(await _set.Has("second"));
    }

    [Fact]
    public async Task DifferentValueTypes_ShouldNotCollide_WhenKeysMatch()
    {
        var stringSet = new TypedSetService<string>(new TypedCacheService<string, bool>(_memoryCache));
        var tupleSet = new TypedSetService<(string, int)>(new TypedCacheService<(string, int), bool>(_memoryCache));

        await stringSet.Set("same-key", Ttl());
        await tupleSet.Set(("same-key", 1), Ttl());

        Assert.True(await stringSet.Has("same-key"));
        Assert.True(await tupleSet.Has(("same-key", 1)));
        Assert.False(await tupleSet.Has(("same-key", 2)));
    }
}
