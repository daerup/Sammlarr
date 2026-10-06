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

	[Test]
	public async Task Webhook_InTheStagingSettings_MayReachTheCatcherOnItsPrivateAddress()
	{
		var webhook = Settings.GetSection(WebhookOptions.SectionName).Get<WebhookOptions>()!;

		await Assert.That(webhook.PrivateHosts).Contains("catcher");
	}

	[Test]
	public async Task Seed_InTheStagingSettings_SignsWebhooksWithAStandardWebhooksSecret()
	{
		var secret = Settings.GetSection("Staging:Seed:Notifications:Webhook").Get<SeedWebhook>()!.Secret;

		await Assert.That(secret).StartsWith("whsec_");
		await Assert.That(Convert.FromBase64String(secret["whsec_".Length..])).Count().IsGreaterThanOrEqualTo(24);
	}

	[Test]
	public async Task Seed_InTheStagingSettings_NamesEveryWantedTitleByAnId()
	{
		var seed = Settings.GetSection("Staging:Seed").Get<StagingSeedData>()!;

		await Assert.That(seed.Wantlist.Select(title => title.Ids().Count)).DoesNotContain(0);
	}

	[Test]
	public async Task Seed_InTheStagingSettings_TagsLibraryTitlesOnlyForItsPersonas()
	{
		var personas = Settings.GetSection("Staging:Personas").Get<StagingPersonas>()!;
		var library = Settings.GetSection("Staging:Seed:Library").Get<SeedLibrary>()!;

		var owners = library.Radarr.Concat(library.Sonarr).Select(title => title.Owner).OfType<string>();

		await Assert.That(owners.Except([personas.Admin.Name, personas.Member.Name])).IsEmpty();
	}
}
