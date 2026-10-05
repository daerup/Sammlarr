namespace Shared.Options;

public record RadarrOptions : IOptionsSection, IProviderHttpOptions
{
	public RadarrAvailability MinimumAvailability { get; set; } = RadarrAvailability.Released;
	public ProviderHttpOptions Http { get; set; } = new();
	public static string SectionName => "Arr:Radarr";
}

public enum RadarrAvailability
{
	Announced,
	InCinemas,
	Released,
}
