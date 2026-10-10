using System.Diagnostics;
using Contracts.Media;
using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Observability.Feed;

namespace Core.Media;

internal sealed record TitleRun(
	Func<MediaType, Optional<MediaProvider>> AddressedBy,
	IReadOnlySet<RemoteService> IdServices,
	IReadOnlySet<RemoteService> DetailServices
)
{
	internal static async Task<TitleRun> StartAsync(ActiveSink sink, MediaIdResolver ids, DetailsFetcher details, CancellationToken cancellationToken)
	{
		var active = await sink.ResolveAsync(cancellationToken);
		var run = await ForIdsAsync(active.AddressedBy, ids, cancellationToken);
		return run with { DetailServices = await details.ConnectedAsync(cancellationToken) };
	}

	internal static async Task<TitleRun> ForIdsAsync(
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		MediaIdResolver ids,
		CancellationToken cancellationToken
	)
	{
		return new TitleRun(addressedBy, await ids.ConnectedAsync(cancellationToken), new HashSet<RemoteService>());
	}

	internal static TracedRequest Traced(IdentifyRequest request) => new(request, TitleSpan(request));

	private static Optional<Activity> TitleSpan(IdentifyRequest request)
	{
		var hints = request.Hints.Or(request.Description);
		return FeedRunSpans.Title(
			hints.Map(found => found.Title).GetValueOr(() => string.Join(", ", request.Candidates)),
			hints.Map(found => found.Year).When(year => year > 0)
		);
	}
}
