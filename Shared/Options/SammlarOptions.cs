namespace Shared.Options;

public record SammlarOptions : IOptionsSection
{
	public required string ConnectionString { get; set; }
	public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(30);
	public string PublicUrl { get; set; } = string.Empty;
	public static string SectionName => "Sammlarr";
}
