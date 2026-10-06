namespace Shared.Options;

public record AccountsOptions : IOptionsSection
{
	public string ResetAdminPassword { get; set; } = string.Empty;

	public string SetupCode { get; set; } = string.Empty;

	public static string SectionName => "Sammlarr:Accounts";
}
