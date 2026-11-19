using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace Codeworx.AspNetCore.Authentication.Introspection.Tests
{
    public class IntrospectionRequestTests
    {
        [Fact]
        public async Task AuthorizedCallWithBearerTokenTriggersIntrospectionRequestAsync()
        {
            var backchannel = new RecordingHttpMessageHandler();
            await using var app = await CreateAppAsync(backchannel);
            var client = app.GetTestClient();

            var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token123");
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(backchannel.LastRequest);
            Assert.Equal(HttpMethod.Post, backchannel.LastRequest.Method);
            Assert.Equal("https://idp.example.com/introspect", backchannel.LastRequest.RequestUri?.ToString());
            Assert.Equal("Basic", backchannel.LastRequest.Headers.Authorization?.Scheme);
            Assert.Contains("token=token123", backchannel.LastRequestBody, StringComparison.Ordinal);
            Assert.Contains("token_type_hint=access_token", backchannel.LastRequestBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CallWithoutBearerTokenDoesNotTriggerIntrospectionRequestAsync()
        {
            var backchannel = new RecordingHttpMessageHandler();
            await using var app = await CreateAppAsync(backchannel);
            var client = app.GetTestClient();

            var response = await client.GetAsync("/secure");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(backchannel.LastRequest);
        }

        private static async Task<WebApplication> CreateAppAsync(RecordingHttpMessageHandler backchannel)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddDataProtection();
            builder.Services.AddAuthorization();
            builder.Services.AddAuthentication(IntrospectionDefaults.AuthenticationScheme)
                .AddIntrospection(options =>
                {
                    options.Audience = "api://test";
                    options.ClientId = "client";
                    options.ClientSecret = "secret";
                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://idp.example.com",
                        IntrospectionEndpoint = "https://idp.example.com/introspect",
                    };
                    options.Backchannel = new HttpClient(backchannel);
                });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/secure", () => "ok").RequireAuthorization();
            await app.StartAsync();
            return app;
        }

        private sealed class RecordingHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            public string? LastRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                LastRequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

                var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
                var json = "{\"active\":true,\"iss\":\"https://idp.example.com\",\"aud\":\"api://test\",\"sub\":\"subject-123\",\"exp\":" + expires + "}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }
        }
    }
}
