namespace Shared.Options;

public record AccountsOptions : IOptionsSection
{
	public Secret ResetAdminPassword { get; set; } = new(string.Empty);

	public Secret SetupCode { get; set; } = new(string.Empty);

	public int MinimumPasswordLength { get; set; } = 12;

	public static string SectionName => "Sammlarr:Accounts";
}
