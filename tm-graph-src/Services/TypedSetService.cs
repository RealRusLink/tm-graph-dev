using Microsoft.Extensions.Caching.Memory;
using tm_graph_src.Services;

namespace tm_graph.Services;

public class TypedSetService<T>(ITypedCache<T, bool> cache) : ITypedSet<T>
{
    public async ValueTask<bool> Has(T value)
    {
        return await cache.KeyExists(value);
    }

    public async ValueTask Set(T value, MemoryCacheEntryOptions options = null)
    {
        if (!(await Has(value)))
        {
            cache.SetValue(value, true, options);
        }
        else
        {
            throw new ArgumentException("Item already exists");
        }
    }

    public async ValueTask Unset(T value)
    {
        await cache.PopValue(value);
    }
}

public interface ITypedSet<T>
{
    ValueTask<bool> Has(T value);

    ValueTask Set(T value, MemoryCacheEntryOptions options);

    ValueTask Unset(T value);
}
