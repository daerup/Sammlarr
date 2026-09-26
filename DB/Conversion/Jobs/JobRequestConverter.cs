using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Jobs;

internal sealed class JobRequestConverter : ValueConverter<IJobRequest, string>
{
	private const string FeedRunPrefix = "feed:";
	private const string WatchlistSyncPrefix = "sync:";

	public JobRequestConverter()
		: base(request => ToText(request), text => FromText(text)) { }

	private static string ToText(IJobRequest request) =>
		request switch
		{
			RunFeed(var id, var trigger) => $"{FeedRunPrefix}{id.Value}:{trigger}",
			SyncWatchlist(var trigger) => $"{WatchlistSyncPrefix}{trigger}",
			_ => throw new NotSupportedException($"{request.GetType().Name} has no stored form"),
		};

	private static IJobRequest FromText(string text) =>
		text.StartsWith(FeedRunPrefix, StringComparison.Ordinal)
			? FeedRunFromText(text[FeedRunPrefix.Length..])
			: new SyncWatchlist(WatchlistSyncTrigger.Parse(text[WatchlistSyncPrefix.Length..]));

	private static IJobRequest FeedRunFromText(string text)
	{
		var separator = text.IndexOf(':', StringComparison.Ordinal);
		return new RunFeed(FeedId.From(Guid.Parse(text[..separator])), FeedRunTrigger.Parse(text[(separator + 1)..]));
	}
}
