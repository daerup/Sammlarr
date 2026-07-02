using Contracts;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;

namespace TestInfrastructure;

public sealed record TestCredential(WatchlistProvider Provider, Guid ClientId, string AccessToken, DateTimeOffset ExpiresAt) : ICredential;

public sealed class TestCredentialFactory : ICredentialFactory
{
	public Task<Result<ICredential>> CreateAsync(
		WatchlistProvider provider,
		Guid clientId,
		string accessToken,
		DateTimeOffset expiresAt,
		CancellationToken cancellationToken = default
	)
	{
		Result<ICredential> result = new TestCredential(provider, clientId, accessToken, expiresAt);
		return Task.FromResult(result);
	}
}
