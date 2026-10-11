namespace Shared.Concurrency;

public sealed record RateWindow(int Requests, TimeSpan Per, bool Aligned = false);

public sealed record LanePolicy(IReadOnlyList<RateWindow> Limits, int MaxInFlight, int FailuresToDown, TimeSpan DownFor)
{
	public static LanePolicy Unlimited { get; } = new([], int.MaxValue, 4, TimeSpan.FromSeconds(30));

	public TimeSpan Pause { get; init; } = TimeSpan.FromSeconds(1);
}

public enum LaneClass
{
	Interactive,
	Source,
	Title,
	Background,
}

public readonly record struct LanePriority(LaneClass Class, long Order)
{
	private static readonly AsyncLocal<LanePriority?> ambient = new();

	private static long issued;

	public static LanePriority Interactive { get; } = new(LaneClass.Interactive, 0);

	public static LanePriority Current => ambient.Value ?? Interactive;

	public static LanePriority Next(LaneClass lane) => new(lane, Interlocked.Increment(ref issued));

	public IDisposable Enter()
	{
		var outer = ambient.Value;
		ambient.Value = this;
		return new Restore(outer);
	}

	internal (int Class, long Order) Rank => ((int)Class, Order);

	private sealed class Restore(LanePriority? outer) : IDisposable
	{
		public void Dispose() => ambient.Value = outer;
	}
}

public interface ILaneQuota
{
	Optional<DateTimeOffset> BlockedUntil(string owner, LaneClass asking, DateTimeOffset now);
}

public readonly record struct LaneLoad(Optional<DateTimeOffset> BlockedUntil, TimeSpan EstimatedWait, bool Down, int InFlight, int Waiting);

public sealed class LaneRefusedException(string reason) : Exception(reason);
