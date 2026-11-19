using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Codeworx.AspNetCore.Authentication.Introspection.Cache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Codeworx.AspNetCore.Authentication.Introspection.Tests
{
    internal static class CacheTestHelpers
    {
        public const string SchemeName = "introspection";

        public static (IntrospectionResponseCache Cache, IDistributedCache Store, IntrospectionOptions Options) CreateCache(Action<IntrospectionOptions>? configure = null)
        {
            var options = new IntrospectionOptions
            {
                ClientId = "client",
                ClientSecret = "secret",
            };

            new IntrospectionPostConfigureOptions(TestDataProtection.CreateProvider()).PostConfigure(SchemeName, options);
            configure?.Invoke(options);

            var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            return (new IntrospectionResponseCache(store, new TestOptionsMonitor(options), NullLogger<IntrospectionResponseCache>.Instance), store, options);
        }

        public static Task<JwtPayload> PayloadFactory(string token, CancellationToken cancellation)
        {
            var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var json = "{\"active\":true,\"sub\":\"subject-" + token + "\",\"exp\":" + expires + "}";
            return Task.FromResult(JwtPayload.Deserialize(json));
        }
    }
}
