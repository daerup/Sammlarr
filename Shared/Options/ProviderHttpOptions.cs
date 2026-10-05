namespace Shared.Options;

public record ProviderHttpOptions
{
	public string HttpVersion { get; set; } = "2.0";
	public bool EnableMultipleHttp2Connections { get; set; } = true;
	public int MaxConcurrency { get; set; }
	public int AttemptTimeoutSeconds { get; set; } = 6;
	public int TotalTimeoutSeconds { get; set; } = 15;
	public int RetryAttempts { get; set; } = 2;
	public bool HonorRetryAfter { get; set; } = true;
	public int MaxWaitSeconds { get; set; }
	public int EnrichWaitSeconds { get; set; }
	public double CircuitBreakerFailureRatio { get; set; } = 0.5;
	public int CircuitBreakerMinimumThroughput { get; set; } = 20;
}
