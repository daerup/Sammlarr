namespace Shared.Options;

public record ReleaseOptions : IOptionsSection, IProviderHttpOptions
{
	public string GitHubRepo { get; set; } = "daerup/Sammlarr";

	public string ApiUrl { get; set; } = "https://api.github.com/";

	public ProviderHttpOptions Http { get; set; } =
		new()
		{
			RetryAttempts = 0,
			AttemptTimeoutSeconds = 5,
			TimeoutSeconds = 5,
		};

	public IReadOnlyDictionary<string, string> Urls => new Dictionary<string, string>(StringComparer.Ordinal);

	public static string SectionName => "Sammlarr:Release";
}
