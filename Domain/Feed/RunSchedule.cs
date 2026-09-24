using Cronos;

namespace Domain.Feed;

public readonly record struct Manual;

public readonly record struct Scheduled
{
	private readonly string _raw;

	public CronExpression CronExpression { get; init; }

	public Scheduled(string cronExpression)
	{
		if (!CronExpression.TryParse(cronExpression, CronFormat.IncludeSeconds, out var parsed))
			throw new ArgumentException($"Invalid cron expression: {cronExpression}", nameof(cronExpression));
		CronExpression = parsed;
		_raw = cronExpression;
	}

	public DateTimeOffset? NextRun(DateTimeOffset from, TimeZoneInfo zone) => CronExpression.GetNextOccurrence(from, zone);

	public override string ToString() => _raw;
}

public union FeedRunTrigger(Manual, Scheduled)
{
	private const string ManualLiteral = "manual";

	public override string ToString() =>
		this switch
		{
			Scheduled scheduled => scheduled.ToString(),
			Manual => ManualLiteral,
		};

	public static FeedRunTrigger Parse(string text) => text == ManualLiteral ? new Manual() : new Scheduled(text);
}