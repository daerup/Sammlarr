using Domain.Providers;

namespace API.Http.Lanes;

internal sealed record RateWindow(int Requests, TimeSpan Per, bool Aligned = false);

internal sealed record LanePolicy(IReadOnlyList<RateWindow> Limits, int MaxInFlight, int FailuresToDown, TimeSpan DownFor)
{
	public static LanePolicy Unlimited { get; } = new([], int.MaxValue, 4, TimeSpan.FromSeconds(30));

	public TimeSpan Pause { get; init; } = TimeSpan.FromSeconds(1);
}

internal interface ILaneQuota
{
	Optional<DateTimeOffset> BlockedUntil(string owner, LaneClass asking, DateTimeOffset now);
}

internal readonly record struct LaneLoad(Optional<DateTimeOffset> BlockedUntil, TimeSpan EstimatedWait, bool Down, int Waiting)
{
	public Optional<LanePace> Paced { get; init; } = new None();
}

internal sealed class LaneRefusedException(string reason) : Exception(reason);
