namespace Shared.Options;

public record SonarrOptions : IOptionsSection, IProviderHttpOptions
{
	public SonarrSeriesType SeriesType { get; set; } = SonarrSeriesType.Standard;
	public bool SeasonFolder { get; set; } = true;
	public ProviderHttpOptions Http { get; set; } = new();
	public static string SectionName => "Arr:Sonarr";
}

public enum SonarrSeriesType
{
	Standard,
	Daily,
	Anime,
}
