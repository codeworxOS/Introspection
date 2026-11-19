using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

namespace Codeworx.AspNetCore.Authentication.Introspection.Tests
{
    public class EncryptCachedPayloadTests
    {
        [Fact]
        public async Task CachedPayloadIsNotStoredAsPlaintextAsync()
        {
            var (cache, store, _) = CacheTestHelpers.CreateCache(o => o.EncryptCachedPayload = true);
            var key = IntrospectionOptions.DefaultCacheKeyProvider("token123");

            var payload = await cache.GetOrAddAsync(CacheTestHelpers.SchemeName, "token123", CacheTestHelpers.PayloadFactory, CancellationToken.None);

            var raw = await store.GetStringAsync(key);
            Assert.NotNull(raw);
            Assert.DoesNotContain("subject-token123", raw, StringComparison.Ordinal);
            Assert.Equal("subject-token123", payload.Sub);
        }

        [Fact]
        public async Task EncryptedPayloadRoundTripsFromCacheAsync()
        {
            var (cache, _, _) = CacheTestHelpers.CreateCache(o => o.EncryptCachedPayload = true);

            await cache.GetOrAddAsync(CacheTestHelpers.SchemeName, "token123", CacheTestHelpers.PayloadFactory, CancellationToken.None);

            var factoryCalls = 0;
            var cached = await cache.GetOrAddAsync(
                CacheTestHelpers.SchemeName,
                "token123",
                (token, cancellation) =>
                {
                    factoryCalls++;
                    return CacheTestHelpers.PayloadFactory(token, cancellation);
                },
                CancellationToken.None);

            Assert.Equal(0, factoryCalls);
            Assert.Equal("subject-token123", cached.Sub);
        }

        [Fact]
        public async Task UnprotectFailureIsTreatedAsCacheMissAsync()
        {
            var (cache, _, options) = CacheTestHelpers.CreateCache(o => o.EncryptCachedPayload = true);

            await cache.GetOrAddAsync(CacheTestHelpers.SchemeName, "token123", CacheTestHelpers.PayloadFactory, CancellationToken.None);

            // Simulate data protection key ring rotation: a new provider can no longer unprotect existing entries.
            options.AccessTokenProtector = TestDataProtection.CreateProvider().CreateProtector(
                "Codeworx.AspNetCore.Authentication.Introspection",
                CacheTestHelpers.SchemeName,
                "AccessToken");

            var factoryCalls = 0;
            var payload = await cache.GetOrAddAsync(
                CacheTestHelpers.SchemeName,
                "token123",
                (token, cancellation) =>
                {
                    factoryCalls++;
                    return CacheTestHelpers.PayloadFactory(token, cancellation);
                },
                CancellationToken.None);

            Assert.Equal(1, factoryCalls);
            Assert.Equal("subject-token123", payload.Sub);
        }
    }
}
