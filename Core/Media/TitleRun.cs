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
	IReadOnlySet<RemoteService> DetailServices,
	IdentityMerge Merge,
	MediaWriteBuffer Buffer
)
{
	internal static async Task<TitleRun> StartAsync(
		ActiveSink sink,
		MediaIdResolver ids,
		DetailsFetcher details,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var active = await sink.ResolveAsync(cancellationToken);
		var run = await ForIdsAsync(active.AddressedBy, ids, buffer, cancellationToken);
		return run with { DetailServices = await details.ConnectedAsync(cancellationToken) };
	}

	internal static async Task<TitleRun> ForIdsAsync(
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		MediaIdResolver ids,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		return new TitleRun(addressedBy, await ids.ConnectedAsync(cancellationToken), new HashSet<RemoteService>(), new IdentityMerge(), buffer);
	}

	internal static TracedRequest Traced(IdentifyRequest request) => new(request, TitleSpan(request));

	internal MediaMatchResult HandOut(MediaMatchResult result)
	{
		return result switch
		{
			MatchedMedia matched => matched with { Identity = matched.Identity with { Id = Buffer.Survivor(matched.Identity.Id) } },
			UnmatchedMedia unmatched => unmatched,
		};
	}

	private static Optional<Activity> TitleSpan(IdentifyRequest request)
	{
		var hints = request.Hints.Or(request.Description);
		return FeedRunSpans.Title(
			hints.Map(found => found.Title).GetValueOr(() => string.Join(", ", request.Candidates)),
			hints.Map(found => found.Year).When(year => year > 0)
		);
	}
}
