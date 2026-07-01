namespace TestInfrastructure;

public sealed record TestApiKey(ApiKeyProvider Provider, string Value) : IApiKey;
