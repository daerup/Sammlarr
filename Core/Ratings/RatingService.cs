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
		var keyed = await Enum.GetValues<ApiKeyProvider>()
			.Where(provider => SuppliesRatings(provider.AsMediaProvider()))
			.ToAsyncEnumerable()
			.Where(async (provider, ct) => await apiKeys.IsStoredAsync(provider, ct))
			.Select(provider => provider.AsMediaProvider())
			.ToListAsync(cancellationToken);
		var signedIn = await Enum.GetValues<AuthProvider>()
			.Where(provider => SuppliesRatings(provider.AsMediaProvider()))
			.ToAsyncEnumerable()
			.Where(async (provider, ct) => await credentials.IsStoredAsync(provider, ct))
			.Select(provider => provider.AsMediaProvider())
			.ToListAsync(cancellationToken);
		return keyed.Concat(signedIn).SuppliedRatingSources();
	}

	private static bool SuppliesRatings(MediaProvider provider) => provider.SuppliedRatingSources().Count > 0;
}
