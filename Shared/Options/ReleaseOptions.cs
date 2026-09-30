namespace Shared.Options;

public record ReleaseOptions : IOptionsSection
{
	public string GitHubRepo { get; set; } = "daerup/Sammlarr";

	public static string SectionName => "Sammlarr:Release";
}
