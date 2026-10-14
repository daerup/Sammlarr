namespace Shared.Options;

public record MdblistOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public IReadOnlyDictionary<string, string> Urls => new Dictionary<string, string>(StringComparer.Ordinal) { ["api"] = BaseUrl };
	public int FeedReserve { get; set; } = 50;
	public double BackgroundReserveShare { get; set; } = 0.5;
	public TimeOnly DailyResetUtc { get; set; } = new(0, 0);
	public static string SectionName => "MDBList";
}
