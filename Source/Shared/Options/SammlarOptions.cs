namespace Shared.Options;

public record SammlarOptions : IOptionsSection
{
	public static string SectionName => "Sammlarr";
	public required string BaseUrl { get; set; }
	public required TimeSpan IdCacheTimeToLive { get; set; }
	public required string ConnectionString { get; set; }
}
