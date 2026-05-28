namespace Shared.Options;

public record SammlarOptions : IOptionsSection
{
	public required string BaseUrl { get; set; }
	public required TimeSpan IdCacheTimeToLive { get; set; }
	public required string ConnectionString { get; set; }
	public static string SectionName => "Sammlarr";
}
