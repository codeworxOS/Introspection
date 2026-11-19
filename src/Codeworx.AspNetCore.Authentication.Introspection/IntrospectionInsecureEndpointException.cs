using System;

namespace Codeworx.AspNetCore.Authentication.Introspection
{
    public class IntrospectionInsecureEndpointException : Exception
    {
        public IntrospectionInsecureEndpointException()
            : this("The introspection endpoint must use HTTPS. Disable RequireHttpsMetadata only for development.")
        {
        }

        public IntrospectionInsecureEndpointException(string? message)
            : base(message)
        {
        }

        public IntrospectionInsecureEndpointException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
