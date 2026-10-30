using Microsoft.Extensions.Caching.Memory;
using tm_graph_src.Services;
using Xunit;

namespace tm_graph_tests;

public class TypedCacheServiceTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly ITypedCache<string, string> _cacheService;

    public TypedCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cacheService = new TypedCacheService<string, string>(_memoryCache);
    }

    [Fact]
    public async Task SetValue_And_PopValue_ShouldReturnAndRemoveValue()
    {
        var key = "test-key";
        var expectedValue = "test-value";

        await _cacheService.SetValue(key, expectedValue);

        var poppedValue = await _cacheService.PopValue(key);

        Assert.Equal(expectedValue, poppedValue);
        
        var secondPopValue = await _cacheService.PopValue(key);
        Assert.Null(secondPopValue);
    }

    [Fact]
    public async Task PopValue_ShouldReturnDefault_WhenKeyDoesNotExist()
    {
        var result = await _cacheService.PopValue("non-existent-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task PopValue_ShouldReturnDefault_WhenStoredTypeDoesNotMatch()
    {
        ITypedCache<string, int> intCacheService = new TypedCacheService<string, int>(_memoryCache);
        
        _memoryCache.Set("wrong-type-key", "string-value");

        var result = await intCacheService.PopValue("wrong-type-key");

        Assert.Equal(default, result);
        Assert.True(_memoryCache.TryGetValue("wrong-type-key", out _));
    }

    [Fact]
    public async Task SetValue_ShouldRespectTtl_WhenExpirationIsSet()
    {
        var key = "ttl-key";
        var value = "ttl-value";
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(50)
        };

        await _cacheService.SetValue(key, value, options);

        var immediateValue = await _cacheService.PopValue(key);
        Assert.Equal(value, immediateValue);

        await _cacheService.SetValue(key, value, options);
        
        await Task.Delay(100);

        var expiredValue = await _cacheService.PopValue(key);
        Assert.Null(expiredValue);
    }
}