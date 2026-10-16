using Contracts.DI;
using Contracts.Feed;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Core.Media;
using Core.Wantlist.Sync;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;

namespace Core.Feed;

internal sealed class FeedRunLayout(
	IFeedService feeds,
	IEnumerable<Keyed<RemoteService, IExternalIdConverter>> converters,
	IServiceReadiness readiness,
	DetailsFetcher details,
	ActiveSink sinks
) : IFeedRunLayout
{
	public async Task<RunLayout> GetAsync(FeedId feed, CancellationToken cancellationToken = default)
	{
		var found = (await feeds.GetAsync(cancellationToken)).Find(feed);
		var hops = IdLookupPlanner.Derive(converters);
		var connected = await readiness.ConnectedAsync(hops.Select(hop => hop.Remote), cancellationToken);
		var sink = await sinks.ResolveAsync(cancellationToken);
		var enrichers = await details.ConnectedAsync(cancellationToken);
		IReadOnlyList<MediaProvider> targets = [.. new[] { MediaType.Movie, MediaType.Show }.Select(sink.AddressedBy).WhereSome().Distinct()];
		IReadOnlyList<Hop> open = [.. hops.Where(hop => connected.Contains(hop.Remote))];
		var layout = found.Match(saved => RunLayouts.Build(open, FeedSourceIds.Of(saved.Feed.Provider), targets, [.. enrichers.Order()]), () => RunLayout.Empty);
		return layout with { Sink = sink.Kind };
	}
}
