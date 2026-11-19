using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Codeworx.AspNetCore.Authentication.Introspection.Cache
{
    public class IntrospectionResponseCache : IIntrospectionResponseCache
    {
        private static readonly TimeSpan _expirationBuffer = TimeSpan.FromSeconds(30);
        private readonly IDistributedCache _distributedCache;
        private readonly IOptionsMonitor<IntrospectionOptions> _optionsMonitor;

        public IntrospectionResponseCache(IDistributedCache distributedCache, IOptionsMonitor<IntrospectionOptions> optionsMonitor)
        {
            _distributedCache = distributedCache;
            _optionsMonitor = optionsMonitor;
        }

        public async Task<JwtPayload> GetOrAddAsync(string scheme, string token, Func<string, CancellationToken, Task<JwtPayload>> payloadFactory, CancellationToken cancellation)
        {
            var options = _optionsMonitor.Get(scheme);
            var cacheKey = options.CacheKeyProvider(token);
            var protector = options.EncryptCachedPayload ? options.AccessTokenProtector : null;

            var cacheResponse = await _distributedCache.GetStringAsync(cacheKey, cancellation);
            JwtPayload? payload = null;

            if (cacheResponse != null)
            {
                if (protector != null)
                {
                    try
                    {
                        cacheResponse = protector.Unprotect(cacheResponse);
                    }
                    catch (CryptographicException)
                    {
                        // e.g. data protection key ring rotation - treat as cache miss
                        cacheResponse = null;
                    }
                }

                if (cacheResponse != null)
                {
                    payload = JwtPayload.Deserialize(cacheResponse);
                }
            }

            if (payload == null)
            {
                payload = await payloadFactory(token, cancellation);

                var json = payload.SerializeToJson();
                var expiresIn = options.CacheDuration;

                var validTo = payload.ValidTo;
                var remainingValidity = validTo == DateTime.MinValue ? expiresIn : (validTo - DateTime.UtcNow + _expirationBuffer);

                if (remainingValidity > TimeSpan.Zero && remainingValidity < expiresIn)
                {
                    expiresIn = remainingValidity;
                }

                var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiresIn };

                await _distributedCache.SetStringAsync(cacheKey, protector?.Protect(json) ?? json, entryOptions, cancellation);
            }

            return payload;
        }
    }
}
