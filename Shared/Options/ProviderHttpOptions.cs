namespace Shared.Options;

public record ProviderHttpOptions
{
	public string HttpVersion { get; set; } = "2.0";
	public bool EnableMultipleHttp2Connections { get; set; } = true;
	public int AttemptTimeoutSeconds { get; set; } = 6;
	public int TimeoutSeconds { get; set; } = 15;
	public int RetryAttempts { get; set; } = 2;
	public int MaxWaitSeconds { get; set; }
	public int EnrichWaitSeconds { get; set; }
	public IReadOnlyDictionary<string, LaneOptions> Lanes { get; set; } = new Dictionary<string, LaneOptions>();
}
