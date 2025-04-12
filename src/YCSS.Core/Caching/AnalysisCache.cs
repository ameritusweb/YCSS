using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Caching
{
    public interface IAnalysisCache
    {
        ValueTask<T?> GetAsync<T>(string key, CancellationToken ct = default);
        ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
        ValueTask RemoveAsync(string key, CancellationToken ct = default);
        ValueTask ClearAsync(CancellationToken ct = default);
    }

    public class MemoryAnalysisCache : IAnalysisCache
    {
        private readonly ILogger<MemoryAnalysisCache> _logger;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly TimeSpan _defaultExpiration;

        public MemoryAnalysisCache(
            ILogger<MemoryAnalysisCache> logger,
            TimeSpan? defaultExpiration = null)
        {
            _logger = logger;
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(30);
        }

        public ValueTask<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired)
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return new ValueTask<T?>((T?)entry.Value);
                }

                _logger.LogDebug("Cache entry expired for key: {Key}", key);
                _cache.TryRemove(key, out _);
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return new ValueTask<T?>(default(T));
        }

        public ValueTask SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken ct = default)
        {
            var entry = new CacheEntry(
                value,
                DateTime.UtcNow.Add(expiration ?? _defaultExpiration)
            );

            _cache.AddOrUpdate(key, entry, (_, _) => entry);
            _logger.LogDebug("Added to cache: {Key}", key);

            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken ct = default)
        {
            _cache.TryRemove(key, out _);
            _logger.LogDebug("Removed from cache: {Key}", key);
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken ct = default)
        {
            _cache.Clear();
            _logger.LogInformation("Cache cleared");
            return ValueTask.CompletedTask;
        }

        private class CacheEntry
        {
            public object Value { get; }
            public DateTime ExpiresAt { get; }
            public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

            public CacheEntry(object value, DateTime expiresAt)
            {
                Value = value;
                ExpiresAt = expiresAt;
            }
        }
    }

    public static class AnalysisCacheExtensions
    {
        public static async ValueTask<T> GetOrCreateAsync<T>(
            this IAnalysisCache cache,
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? expiration = null,
            CancellationToken ct = default)
        {
            var cached = await cache.GetAsync<T>(key, ct);
            if (cached != null)
            {
                return cached;
            }

            var value = await factory(ct);
            await cache.SetAsync(key, value, expiration, ct);
            return value;
        }
    }
}
