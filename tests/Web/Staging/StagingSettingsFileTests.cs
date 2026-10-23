using System.Text.Json.Nodes;
using Domain.Providers;
using Microsoft.Extensions.Configuration;
using Shared.Options;
using TestInfrastructure;
using Web.Staging;

namespace Web.Tests.Staging;

public class StagingSettingsFileTests
{
	private static readonly IConfiguration Settings = new ConfigurationBuilder()
		.AddJsonFile(RepositoryRoot.Resolve("src", "AppHost", "appsettings.Staging.json"))
		.Build();

	[Test]
	public async Task Personas_InTheStagingSettings_MeetTheirOwnMinimumPasswordLength()
	{
		var personas = Settings.GetSection("Staging:Personas").Get<StagingPersonas>()!;
		var minimum = Settings.GetSection(AccountsOptions.SectionName).Get<AccountsOptions>()!.MinimumPasswordLength;

		await Assert.That(Math.Min(personas.Admin.Password.Length, personas.Member.Password.Length)).IsGreaterThanOrEqualTo(minimum);
	}

	[Test]
	public async Task Plex_InTheStagingSettings_GoesThroughWireMockWhichProxiesTheReadsButNeverTheWatchlistWrites()
	{
		var plex = Settings.GetSection(PlexOptions.SectionName);
		string[] reads = [nameof(PlexOptions.BaseUrl), nameof(PlexOptions.DiscoverUrl), nameof(PlexOptions.MetadataUrl)];
		var mappings = JsonNode.Parse(File.ReadAllText(RepositoryRoot.Resolve("src", "AppHost", "Staging", "wiremock", "plex.json")))!["mappings"]!.AsArray();

		var proxied = mappings.Select(mapping => mapping!["response"]!["proxyUrlPrefixToRemove"]?.GetValue<string>()).OfType<string>();

		await Assert.That(proxied).IsEquivalentTo(reads.Select(read => new Uri(plex[read]!).AbsolutePath));
		await Assert.That(plex[nameof(PlexOptions.VodUrl)]).StartsWith("http://wiremock:8080/plex/");
	}

	public static IEnumerable<string> DataSources() =>
		[
			TmdbOptions.SectionName,
			MdblistOptions.SectionName,
			WikidataOptions.SectionName,
			TvmazeOptions.SectionName,
			ImdbDatasetsOptions.SectionName,
			AnimeListsOptions.SectionName,
			AniListOptions.SectionName,
			MalOptions.SectionName,
			TraktOptions.SectionName,
		];

	[Test]
	[MethodDataSource(nameof(DataSources))]
	public async Task Network_InTheStagingSettings_LetsTheDataSourceThroughTheDeadProxy(string section)
	{
		var app = new ConfigurationBuilder().AddJsonFile(RepositoryRoot.Resolve("src", "Web", "appsettings.json")).Build();
		var reachable = Settings.GetSection("Staging:Network:Reachable").Get<string[]>()!;

		await Assert.That(reachable).Contains(new Uri(app[$"{section}:BaseUrl"]!).Host);
	}

	[Test]
	public async Task Seed_InTheStagingSettings_FollowsAListOnEveryFeedProvider()
	{
		var feeds = Settings.GetSection("Staging:Seed:Feeds").Get<SeedFeed[]>()!;

		await Assert.That(feeds.Select(feed => feed.Provider).Distinct()).IsEquivalentTo(Enum.GetValues<FeedProvider>());
	}

	[Test]
	public async Task Seed_InTheStagingSettings_SignsWebhooksWithAStandardWebhooksSecret()
	{
		var secret = Settings.GetSection("Staging:Seed:Notifications:Webhook").Get<SeedWebhook>()!.Secret.Reveal();

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
