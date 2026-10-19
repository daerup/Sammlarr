using Domain.Providers;

namespace Harness;

public sealed record SeedKeys(IReadOnlyDictionary<ApiKeyProvider, string> ApiKeys, IReadOnlyDictionary<AuthProvider, string> Tokens);
