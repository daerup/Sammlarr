namespace Shared.Options;

public record AccountsOptions : IOptionsSection
{
	public string ResetAdminPassword { get; set; } = string.Empty;

	public string SetupCode { get; set; } = string.Empty;

	public int MinimumPasswordLength { get; set; } = 12;

	public static string SectionName => "Sammlarr:Accounts";
}
