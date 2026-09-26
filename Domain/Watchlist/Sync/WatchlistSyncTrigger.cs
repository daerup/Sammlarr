namespace Domain.Watchlist.Sync;

public union WatchlistSyncTrigger(ManualTrigger, ScheduledTrigger)
{
	private const string ManualLiteral = "Manual";
	private const string ScheduledLiteral = "Scheduled";

	public override string ToString() =>
		this switch
		{
			ManualTrigger => ManualLiteral,
			ScheduledTrigger => ScheduledLiteral,
		};

	public static WatchlistSyncTrigger Parse(string text) => text == ScheduledLiteral ? new ScheduledTrigger() : new ManualTrigger();
}

public readonly record struct ManualTrigger;

public readonly record struct ScheduledTrigger;
