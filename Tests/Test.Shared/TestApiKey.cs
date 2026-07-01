namespace Test.Shared;

public sealed record TestApiKey(ApiKeyProvider Provider, string Value) : IApiKey;
