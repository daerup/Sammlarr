using Domain.Auth;
using Domain.Providers;

namespace TestInfrastructure;

public sealed record TestApiKey(ApiKeyProvider Provider, string Value) : IApiKey;
