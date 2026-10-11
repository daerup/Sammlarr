namespace Shared.Options;

public record FeedLoadOptions : IOptionsSection
{
	public TimeSpan StallAfter { get; set; } = TimeSpan.FromMinutes(3);

	public static string SectionName => "Sammlarr:FeedLoad";
}
