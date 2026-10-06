using Contracts;
using Domain.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed record StoredApiKey(ApiKeyProvider Provider, [property: Sensitive] string Value) : IApiKey;
