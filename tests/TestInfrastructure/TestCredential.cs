using Domain.Auth;
using Domain.Providers;

namespace TestInfrastructure;

public sealed record TestCredential(AuthProvider Provider, Guid ClientId, Secret AccessToken, DateTimeOffset ExpiresAt) : ICredential
{
	public TestCredential(AuthProvider provider, Guid clientId, string accessToken, DateTimeOffset expiresAt)
		: this(provider, clientId, new Secret(accessToken), expiresAt) { }

	public static implicit operator Result<ICredential>(TestCredential credential) => Success.Of<ICredential>(credential);
}
