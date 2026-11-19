using System;
using Microsoft.Extensions.Options;

namespace Codeworx.AspNetCore.Authentication.Introspection.Tests
{
    internal sealed class TestOptionsMonitor : IOptionsMonitor<IntrospectionOptions>
    {
        public TestOptionsMonitor(IntrospectionOptions options)
        {
            CurrentValue = options;
        }

        public IntrospectionOptions CurrentValue { get; }

        public IntrospectionOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<IntrospectionOptions, string?> listener) => null;
    }
}
