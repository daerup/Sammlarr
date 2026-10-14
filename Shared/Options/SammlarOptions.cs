namespace Shared.Options;

public record SammlarOptions : IOptionsSection
{
	public required string ConnectionString { get; set; }
	public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(30);
	public static string SectionName => "Sammlarr";
}
