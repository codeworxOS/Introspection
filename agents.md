# Coding Agent Instructions

This file provides guidance to agents when working with code in this repository.
The name of the solution is Codeworx.AspNetCore.Authentication.Introspection.

The solution is an implementation of the OAuth2 Introspection RFC specification for use with ASP.NET Core.
It implements an authentication handler that takes the bearer token from the `Authorization` HTTP header and calls an OAuth2 introspection endpoint to verify the token's validity.

The RFC specification can be found at: https://www.rfc-editor.org/info/rfc7662/

The implementation is inspired by the [Microsoft.AspNetCore.Authentication.JwtBearer package](https://github.com/dotnet/aspnetcore/tree/main/src/Security/Authentication/JwtBearer/src) and adds the introspection lookup capability.

## Technology stack
- ASP.NET Core with .NET 10.0
- xUnit for integration and unit testing in .NET

## Dev environment tips
- [ ] Do not forget to run `apm install` before loading any skills.
- [ ] Always use local dotnet tools instead of global ones.

## Deployment
- [ ] For CI/CD pipelines or Microsoft/Azure resource work, use the `microsoft-docs` and `microsoft-code-reference` skills to verify current guidance and API usage.
- [ ] In YAML files under `deploy`, use explicit `PowerShell@2` tasks for PowerShell commands instead of Azure Pipelines `script`, `powershell`, or `pwsh` shortcut steps.
