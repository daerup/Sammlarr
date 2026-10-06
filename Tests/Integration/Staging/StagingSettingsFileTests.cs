using Microsoft.Extensions.Configuration;
using Shared.Options;
using TestInfrastructure;
using Web.Staging;

namespace Integration.Tests.Staging;

public class StagingSettingsFileTests
{
	private static readonly IConfiguration Settings = new ConfigurationBuilder()
		.AddJsonFile(RepositoryRoot.Resolve("AppHost", "appsettings.Staging.json"))
		.Build();

	[Test]
	public async Task Personas_InTheStagingSettings_MeetTheirOwnMinimumPasswordLength()
	{
		var personas = Settings.GetSection("Staging:Personas").Get<StagingPersonas>()!;
		var minimum = Settings.GetSection(AccountsOptions.SectionName).Get<AccountsOptions>()!.MinimumPasswordLength;

		await Assert.That(Math.Min(personas.Admin.Password.Length, personas.Member.Password.Length)).IsGreaterThanOrEqualTo(minimum);
	}
}
