namespace Shared.Options;

public record SammlarOptions : IOptionsSection
{
	public required string ConnectionString { get; set; }
	public static string SectionName => "Sammlarr";
}
