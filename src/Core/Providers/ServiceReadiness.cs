using Contracts.Auth;
using Contracts.Providers;
using Contracts.Settings;
using Core.Auth;
using Domain.Auth;
using Domain.Providers;

namespace Core.Providers;

internal sealed class ServiceReadiness(
	IApiKeyConfigRepository keys,
	ICredentialRepository credentials,
	IArrSinkConfigService arr,
	SecretCache<ApiKeyProvider, IApiKey> keyChecks,
	SecretCache<AuthProvider, ICredential> signInChecks
) : IServiceReadiness
{
	public async Task<IReadOnlySet<RemoteService>> ConnectedAsync(IEnumerable<RemoteService> remotes, CancellationToken cancellationToken = default)
	{
		return await remotes
			.Distinct()
			.ToAsyncEnumerable()
			.Where(async (remote, ct) => await ConnectedAsync(remote, ct))
			.ToHashSetAsync(cancellationToken: cancellationToken);
	}

	public async Task<bool> ReadyAsync(Service service, CancellationToken cancellationToken = default)
	{
		var remotes = service.Remotes;
		return (await ConnectedAsync(remotes, cancellationToken)).Count == remotes.Count;
	}

	private async Task<bool> ConnectedAsync(RemoteService remote, CancellationToken cancellationToken)
	{
		return remote.Access switch
		{
			Open => true,
			UserKey(var slot) => !keyChecks.IsRejected(slot) && await keys.GetAsync(slot, cancellationToken).IsSuccess(),
			UserSignIn(var signIn) => !signInChecks.IsRejected(signIn) && await credentials.GetAsync(signIn, cancellationToken).IsSuccess(),
			AppInstance(var app) => (await arr.GetAsync(cancellationToken)).Of(app).IsConnectable,
		};
	}
}
