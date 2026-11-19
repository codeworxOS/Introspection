using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Codeworx.AspNetCore.Authentication.Introspection.Tests
{
    internal static class TestDataProtection
    {
        public static IDataProtectionProvider CreateProvider()
        {
            var keyDirectory = Path.Combine(Path.GetTempPath(), "introspection-tests", Guid.NewGuid().ToString("N"));
            var services = new ServiceCollection()
                .AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
                .Services
                .BuildServiceProvider();
            return services.GetRequiredService<IDataProtectionProvider>();
        }
    }
}
