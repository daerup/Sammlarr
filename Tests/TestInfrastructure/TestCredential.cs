using Contracts;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;

namespace TestInfrastructure;

public sealed record TestCredential(AuthProvider Provider, Guid ClientId, string AccessToken, DateTimeOffset ExpiresAt) : ICredential
{
	public static implicit operator Result<ICredential>(TestCredential credential) => Success.Of<ICredential>(credential);
}

public sealed class TestCredentialFactory : ICredentialFactory
{
	public Task<Result<ICredential>> CreateAsync(
		AuthProvider provider,
		Guid clientId,
		string accessToken,
		DateTimeOffset expiresAt,
		CancellationToken cancellationToken = default
	)
	{
		Result<ICredential> result = Success.Of<ICredential>(new TestCredential(provider, clientId, accessToken, expiresAt));
		return Task.FromResult(result);
	}
}
