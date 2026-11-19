using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace Codeworx.AspNetCore.Authentication.Introspection.Tests
{
    public class HttpsMetadataRequiredTests
    {
        [Fact]
        public async Task HttpEndpointThrowsWhenHttpsIsRequiredAsync()
        {
            var options = CreateOptions("http://example.com/introspect", requireHttps: true);
            var handler = await CreateHandlerAsync(options);

            await Assert.ThrowsAsync<IntrospectionInsecureEndpointException>(() => handler.CallEndpointAsync("token"));
        }

        [Fact]
        public async Task HttpEndpointIsAllowedWhenHttpsIsNotRequiredAsync()
        {
            var options = CreateOptions("http://example.com/introspect", requireHttps: false);
            var handler = await CreateHandlerAsync(options);

            var payload = await handler.CallEndpointAsync("token");

            Assert.Equal("subject-token", payload.Sub);
        }

        [Fact]
        public async Task HttpsEndpointIsAllowedWhenHttpsIsRequiredAsync()
        {
            var options = CreateOptions("https://example.com/introspect", requireHttps: true);
            var handler = await CreateHandlerAsync(options);

            var payload = await handler.CallEndpointAsync("token");

            Assert.Equal("subject-token", payload.Sub);
        }

        private static IntrospectionOptions CreateOptions(string endpoint, bool requireHttps)
        {
            var options = new IntrospectionOptions
            {
                Configuration = new OpenIdConnectConfiguration { IntrospectionEndpoint = endpoint },
                ClientId = "client",
                ClientSecret = "secret",
                RequireHttpsMetadata = requireHttps,
                Backchannel = new HttpClient(new StubHttpMessageHandler()),
            };

            new IntrospectionPostConfigureOptions(TestDataProtection.CreateProvider()).PostConfigure("introspection", options);
            return options;
        }

        private static async Task<TestableHandler> CreateHandlerAsync(IntrospectionOptions options)
        {
            var handler = new TestableHandler(new TestOptionsMonitor(options));
            await handler.InitializeAsync(new AuthenticationScheme("introspection", null, typeof(TestableHandler)), new DefaultHttpContext());
            return handler;
        }

        private sealed class TestableHandler : IntrospectionAuthenticationHandler
        {
            public TestableHandler(IOptionsMonitor<IntrospectionOptions> options)
                : base(options, NullLoggerFactory.Instance, System.Text.Encodings.Web.UrlEncoder.Default, new NullResponseCache())
            {
            }

            public Task<JwtPayload> CallEndpointAsync(string token) => CallIntrospectionEndpointAsync(token, CancellationToken.None);
        }

        private sealed class NullResponseCache : IIntrospectionResponseCache
        {
            public Task<JwtPayload> GetOrAddAsync(string scheme, string token, Func<string, CancellationToken, Task<JwtPayload>> payloadFactory, CancellationToken cancellation)
                => throw new NotSupportedException();
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
                var json = "{\"active\":true,\"sub\":\"subject-token\",\"exp\":" + expires + "}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                });
            }
        }
    }
}
