using Contracts.Providers;
using Contracts.Ratings;
using Domain.Media;
using Domain.Providers;

namespace Core.Ratings;

internal sealed class RatingService(IRatingDefaultRepository defaults, IServiceReadiness readiness) : IRatingService
{
	private static readonly RatingStrategy Fallback = new(AveragedRating.Everything);

	public async Task<RatingStrategy> GetDefaultAsync(CancellationToken cancellationToken = default)
	{
		return (await defaults.FindAsync(cancellationToken)).GetValueOr(Fallback);
	}

	public Task StoreDefaultAsync(RatingStrategy strategy, CancellationToken cancellationToken = default) => defaults.StoreAsync(strategy, cancellationToken);

	public async Task<ValueSet<RatingSource>> GetAvailableAsync(CancellationToken cancellationToken = default)
	{
		var suppliers = Enum.GetValues<RemoteService>().Where(remote => remote.SuppliedRatingSources().Count > 0);
		return (await readiness.ConnectedAsync(suppliers, cancellationToken)).SuppliedRatingSources();
	}
}
