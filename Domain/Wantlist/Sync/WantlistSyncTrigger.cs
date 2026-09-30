namespace Domain.Wantlist.Sync;

public union WantlistSyncTrigger(ManualTrigger, ScheduledFeedTrigger)
{
	private const string ManualLiteral = "Manual";
	private const string ScheduledFeedLiteral = "ScheduledFeed";

	public override string ToString() =>
		this switch
		{
			ManualTrigger => ManualLiteral,
			ScheduledFeedTrigger => ScheduledFeedLiteral,
		};

	public static WantlistSyncTrigger Parse(string text) => text == ScheduledFeedLiteral ? new ScheduledFeedTrigger() : new ManualTrigger();
}

public readonly record struct ManualTrigger;

public readonly record struct ScheduledFeedTrigger;
