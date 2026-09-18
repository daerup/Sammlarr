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

	public override string ToString() => _raw;
}

public union SyncMode(Manual, Scheduled);