using Microsoft.Extensions.Caching.Memory;

namespace tm_graph_src.Services;

public class TypedCacheService<TKey, TValue>(IMemoryCache cache): ITypedCache<TKey, TValue>
{
    
    public ValueTask<TValue?> PopValue(TKey key)
    {
        cache.TryGetValue(key!, out var value);
        if (value is TValue) cache.Remove(key!);
        return ValueTask.FromResult(value is TValue v ? v : default);
    }
    
    public ValueTask<TValue?> GetValue(TKey key)
    {
        cache.TryGetValue(key!, out var value);
        return ValueTask.FromResult(value is TValue v ? v : default);
    }
    public ValueTask SetValue(TKey key, TValue value, MemoryCacheEntryOptions? options = null)
    {
        cache.Set(key!, value, options);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> KeyExists(TKey key)
    {
        cache.TryGetValue(key!, out var value);
        return ValueTask.FromResult(value is TValue);
    }
}


        
public interface ITypedCache<TKey, TValue>
{
    
    public ValueTask<TValue?> PopValue(TKey key);
    public ValueTask<TValue?> GetValue(TKey key);
    public ValueTask<bool> KeyExists(TKey key);
    public ValueTask SetValue(TKey key, TValue value, MemoryCacheEntryOptions? options = null);
}