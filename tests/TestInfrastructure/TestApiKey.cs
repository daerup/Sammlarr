using Domain.Auth;
using Domain.Providers;

namespace TestInfrastructure;

public sealed record TestApiKey(ApiKeyProvider Provider, Secret Value) : IApiKey
{
	public TestApiKey(ApiKeyProvider provider, string value)
		: this(provider, new Secret(value)) { }
}
