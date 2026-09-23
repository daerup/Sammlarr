using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Jobs;

internal sealed class JobRequestConverter : ValueConverter<IJobRequest, string>
{
	private const string FeedRunPrefix = "feed:";
	private const string WatchlistSyncPrefix = "sync:";
	private const string ManualLiteral = "manual";

	public JobRequestConverter()
		: base(request => ToText(request), text => FromText(text)) { }

	private static string ToText(IJobRequest request) =>
		request switch
		{
			RunFeed(var id, var trigger) => $"{FeedRunPrefix}{id.Value}:{TriggerToText(trigger)}",
			SyncWatchlist(var trigger) => $"{WatchlistSyncPrefix}{trigger}",
			_ => throw new NotSupportedException($"{request.GetType().Name} has no stored form"),
		};

	private static IJobRequest FromText(string text) =>
		text.StartsWith(FeedRunPrefix, StringComparison.Ordinal) ? FeedRunFromText(text[FeedRunPrefix.Length..]) : new SyncWatchlist(new ManualTrigger());

	private static IJobRequest FeedRunFromText(string text)
	{
		var separator = text.IndexOf(':', StringComparison.Ordinal);
		var id = FeedId.From(Guid.Parse(text[..separator]));
		var trigger = text[(separator + 1)..];
		return new RunFeed(id, trigger == ManualLiteral ? new Manual() : new Scheduled(trigger));
	}

	private static string TriggerToText(FeedRunTrigger trigger) =>
		trigger switch
		{
			Scheduled scheduled => scheduled.ToString(),
			Manual => ManualLiteral,
		};
}
