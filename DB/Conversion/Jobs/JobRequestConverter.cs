using Domain.Feed;
using Domain.Jobs;
using Domain.Wantlist.Sync;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Jobs;

internal sealed class JobRequestConverter : ValueConverter<IJobRequest, string>
{
	private const string FeedRunPrefix = "feed:";
	private const string WantlistSyncPrefix = "sync:";
	private const string SyncAfterMarker = "+sync ";

	public JobRequestConverter()
		: base(request => ToText(request), text => FromText(text)) { }

	private static string ToText(IJobRequest request) =>
		request switch
		{
			RunFeed(var id, var trigger, var syncAfter) => $"{FeedRunPrefix}{id.Value}:{(syncAfter ? SyncAfterMarker : string.Empty)}{trigger}",
			SyncWantlist(var trigger) => $"{WantlistSyncPrefix}{trigger}",
			_ => throw new NotSupportedException($"{request.GetType().Name} has no stored form"),
		};

	private static IJobRequest FromText(string text) =>
		text.StartsWith(FeedRunPrefix, StringComparison.Ordinal)
			? FeedRunFromText(text[FeedRunPrefix.Length..])
			: new SyncWantlist(WantlistSyncTrigger.Parse(text[WantlistSyncPrefix.Length..]));

	private static IJobRequest FeedRunFromText(string text)
	{
		var separator = text.IndexOf(':', StringComparison.Ordinal);
		var rest = text[(separator + 1)..];
		var syncAfter = rest.StartsWith(SyncAfterMarker, StringComparison.Ordinal);
		return new RunFeed(FeedId.From(Guid.Parse(text[..separator])), FeedRunTrigger.Parse(syncAfter ? rest[SyncAfterMarker.Length..] : rest), syncAfter);
	}
}
