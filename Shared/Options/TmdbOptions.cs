namespace Shared.Options;

public record TmdbOptions : IOptionsSection
{
	public required string BaseUrl { get; set; }
	public required string ImageBaseUrl { get; set; }
	public static string SectionName => "TMDB";
}
