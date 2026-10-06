using Contracts.Auth;
using Contracts.Ratings;
using Domain.Media;
using Domain.Providers;

namespace Core.Ratings;

internal sealed class RatingService(IRatingDefaultRepository defaults, IApiKeyService apiKeys, ICredentialService credentials) : IRatingService
{
	private static readonly RatingStrategy Fallback = new(AveragedRating.Everything);

	public async Task<RatingStrategy> GetDefaultAsync(CancellationToken cancellationToken = default)
	{
		return (await defaults.FindAsync(cancellationToken)).GetValueOr(Fallback);
	}

	public Task StoreDefaultAsync(RatingStrategy strategy, CancellationToken cancellationToken = default) => defaults.StoreAsync(strategy, cancellationToken);

	public async Task<ValueSet<RatingSource>> GetAvailableAsync(CancellationToken cancellationToken = default)
	{
		var connected = await Enum.GetValues<RemoteService>()
			.Where(remote => remote.SuppliedRatingSources().Count > 0)
			.ToAsyncEnumerable()
			.Where(async (remote, ct) => await StoredAsync(remote, ct))
			.ToListAsync(cancellationToken);
		return connected.SuppliedRatingSources();
	}

	private async Task<bool> StoredAsync(RemoteService remote, CancellationToken cancellationToken)
	{
		return remote.Access switch
		{
			UserKey(var slot) => await apiKeys.IsStoredAsync(slot, cancellationToken),
			UserSignIn(var signIn) => await credentials.IsStoredAsync(signIn, cancellationToken),
			Open or AppInstance => true,
		};
	}
}
