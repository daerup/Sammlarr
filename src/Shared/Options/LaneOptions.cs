namespace Shared.Options;

public record LaneOptions
{
	public IReadOnlyList<LaneRouteOptions> Routes { get; set; } = [];
	public IReadOnlyList<RateWindowOptions> Limits { get; set; } = [];
	public int MaxInFlight { get; set; } = int.MaxValue;
	public int MaxBatch { get; set; } = 1;
	public int FailuresToDown { get; set; } = 4;
	public int DownSeconds { get; set; } = 30;
}

public record LaneRouteOptions
{
	public string Url { get; set; } = string.Empty;
	public string Path { get; set; } = string.Empty;
}

public record RateWindowOptions
{
	public int Requests { get; set; }
	public TimeSpan Per { get; set; }
	public bool Aligned { get; set; }
}
