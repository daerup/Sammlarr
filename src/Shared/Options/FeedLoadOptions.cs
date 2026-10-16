namespace Shared.Options;

public record FeedLoadOptions : IOptionsSection
{
	public TimeSpan StallAfter { get; set; } = TimeSpan.FromMinutes(3);

	public TimeSpan ShownWait { get; set; } = TimeSpan.FromSeconds(5);

	public static string SectionName => "Sammlarr:FeedLoad";
}
