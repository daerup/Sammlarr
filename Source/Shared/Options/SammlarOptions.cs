namespace Shared.Options;

public sealed record SammlarOptions : IOptionsSection
{
	public static string SectionName => "Sammlarr";
	public required string BaseUrl { get; set; }
	public required TimeSpan IdCacheTimeToLive { get; set; }
}
