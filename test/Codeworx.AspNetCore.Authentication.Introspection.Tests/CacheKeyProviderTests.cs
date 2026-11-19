using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

namespace Codeworx.AspNetCore.Authentication.Introspection.Tests
{
    public class CacheKeyProviderTests
    {
        [Fact]
        public async Task DefaultCacheKeyProviderHashesTheTokenAsync()
        {
            var (cache, store, _) = CacheTestHelpers.CreateCache();

            await cache.GetOrAddAsync(CacheTestHelpers.SchemeName, "token123", CacheTestHelpers.PayloadFactory, CancellationToken.None);

            var expectedKey = IntrospectionOptions.DefaultCacheKeyProvider("token123");
            Assert.StartsWith("introspection:", expectedKey, StringComparison.Ordinal);
            Assert.NotNull(await store.GetStringAsync(expectedKey));
            Assert.Null(await store.GetStringAsync("token123"));
        }

        [Fact]
        public async Task CustomCacheKeyProviderIsUsedAsync()
        {
            var (cache, store, _) = CacheTestHelpers.CreateCache(o => o.CacheKeyProvider = token => "custom:" + token);

            await cache.GetOrAddAsync(CacheTestHelpers.SchemeName, "token123", CacheTestHelpers.PayloadFactory, CancellationToken.None);

            Assert.NotNull(await store.GetStringAsync("custom:token123"));
            Assert.Null(await store.GetStringAsync(IntrospectionOptions.DefaultCacheKeyProvider("token123")));
        }
    }
}
