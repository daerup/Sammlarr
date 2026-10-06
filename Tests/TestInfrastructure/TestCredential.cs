using Domain.Auth;
using Domain.Providers;

namespace TestInfrastructure;

public sealed record TestCredential(AuthProvider Provider, Guid ClientId, string AccessToken, DateTimeOffset ExpiresAt) : ICredential
{
	public static implicit operator Result<ICredential>(TestCredential credential) => Success.Of<ICredential>(credential);
}
