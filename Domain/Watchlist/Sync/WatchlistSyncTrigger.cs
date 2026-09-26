namespace Domain.Watchlist.Sync;

public union WatchlistSyncTrigger(ManualTrigger, ScheduledFeedTrigger)
{
	private const string ManualLiteral = "Manual";
	private const string ScheduledFeedLiteral = "ScheduledFeed";

	public override string ToString() =>
		this switch
		{
			ManualTrigger => ManualLiteral,
			ScheduledFeedTrigger => ScheduledFeedLiteral,
		};

	public static WatchlistSyncTrigger Parse(string text) => text == ScheduledFeedLiteral ? new ScheduledFeedTrigger() : new ManualTrigger();
}

public readonly record struct ManualTrigger;

public readonly record struct ScheduledFeedTrigger;
