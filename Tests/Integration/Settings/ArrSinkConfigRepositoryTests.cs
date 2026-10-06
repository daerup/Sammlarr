using Contracts.Settings;
using DB.Data.Config;
using Domain.Wantlist.Arr;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Settings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ArrSinkConfigRepositoryTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string RadarrKey = "0123456789abcdef0123456789abcdef";

	private const string SonarrKey = "fedcba9876543210fedcba9876543210";

	private static readonly ArrSinkConfig Connected = new(
		new ArrInstanceConfig
		{
			Url = Some.Of("http://radarr:7878"),
			ExternalUrl = Some.Of("http://localhost:7879"),
			ApiKey = Some.Of(RadarrKey),
			QualityProfileId = Some.Of(4),
			RootFolderPath = Some.Of("/movies"),
			Monitored = true,
			SearchOnAdd = false,
		},
		new ArrInstanceConfig
		{
			Url = Some.Of("http://sonarr:8989"),
			ApiKey = Some.Of(SonarrKey),
			QualityProfileId = Some.Of(6),
			RootFolderPath = Some.Of("/tv"),
			Monitored = false,
			SearchOnAdd = true,
		},
		ArrTagScheme.Default with
		{
			Prefix = "media",
			FeedSource = ArrFeedTagSource.OwnerAndList,
			PlainPrefix = true,
		}
	);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IArrSinkConfigRepository Testee => new ArrSinkConfigRepository(host.ContextFactory);

	[Test]
	public async Task GetAsync_OnAFreshDatabase_HasNothingConnectedAndTheDefaultTags()
	{
		await Assert.That(await Testee.GetAsync()).IsEqualTo(ArrSinkConfig.Empty);
	}

	[Test]
	public async Task StoreAsync_ThenGetAsync_ReturnsWhatWasStored()
	{
		await Testee.StoreAsync(Connected);

		await Assert.That(await Testee.GetAsync()).IsEqualTo(Connected);
	}

	[Test]
	public async Task StoreAsync_KeepsEachAppsApiKeyEncryptedInItsOwnColumn()
	{
		await Testee.StoreAsync(Connected);

		var radarr = await host.Context.Database.SqlQuery<string>($"""SELECT "Radarr_ApiKey" AS "Value" FROM "ProviderConfig" """).SingleAsync();
		var sonarr = await host.Context.Database.SqlQuery<string>($"""SELECT "Sonarr_ApiKey" AS "Value" FROM "ProviderConfig" """).SingleAsync();

		await Assert.That(radarr).IsNotEqualTo(RadarrKey).And.DoesNotContain(RadarrKey);
		await Assert.That(sonarr).IsNotEqualTo(SonarrKey).And.DoesNotContain(SonarrKey);
	}
}
