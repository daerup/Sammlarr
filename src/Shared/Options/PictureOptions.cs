namespace Shared.Options;

public record PictureOptions : IOptionsSection, IProviderHttpOptions
{
	public ProviderHttpOptions Http { get; set; } =
		new()
		{
			RetryAttempts = 0,
			AttemptTimeoutSeconds = 3,
			TimeoutSeconds = 3,
		};
	public IReadOnlyDictionary<string, string> Urls => new Dictionary<string, string>();
	public static string SectionName => "Pictures";
}
