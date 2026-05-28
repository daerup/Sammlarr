using Cronos;

namespace Contracts.Feed;

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

public union SyncMode(Manual manual, Scheduled scheduled)
{
	private const string manual = "manual";

	public override string ToString()
	{
		return Value switch
		{
			Scheduled s => s.ToString(),
			_ => manual,
		};
	}

	public static SyncMode From(string value)
	{
		return value switch
		{
			manual => new SyncMode(new Manual()),
			_ => new SyncMode(new Scheduled(value)),
		};
	}
}
