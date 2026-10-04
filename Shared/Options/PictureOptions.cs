namespace Shared.Options;

public record PictureOptions : IOptionsSection, IProviderHttpOptions
{
	public ProviderHttpOptions Http { get; set; } =
		new()
		{
			RetryAttempts = 0,
			AttemptTimeoutSeconds = 3,
			TotalTimeoutSeconds = 3,
		};
	public static string SectionName => "Pictures";
}
