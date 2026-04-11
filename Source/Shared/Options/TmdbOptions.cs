namespace Shared.Options;

public record TmdbOptions : IOptionsSection
{
	public static string SectionName => "TMDB";

	public required string BaseUrl { get; set; }
	public required string ImageBaseUrl { get; set; }
}
