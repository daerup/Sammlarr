using Contracts.Auth;
using Domain.Providers;

namespace UI.Shared.Services;

internal static class ProviderReadiness
{
	extension(IApiKeyService keys)
	{
		internal async Task<IReadOnlySet<ApiKeyProvider>> GetReadyAsync(CancellationToken cancellationToken = default)
		{
			return await Enum.GetValues<ApiKeyProvider>()
				.ToAsyncEnumerable()
				.Where(async (provider, ct) => await keys.GetAsync(provider, ct).IsSuccess())
				.ToHashSetAsync(cancellationToken: cancellationToken);
		}
	}

	extension(ICredentialService credentials)
	{
		internal async Task<IReadOnlySet<AuthProvider>> GetReadyAsync(CancellationToken cancellationToken = default)
		{
			return await Enum.GetValues<AuthProvider>()
				.ToAsyncEnumerable()
				.Where(async (provider, ct) => await credentials.GetAsync(provider, ct).IsSuccess())
				.ToHashSetAsync(cancellationToken: cancellationToken);
		}
	}
}
