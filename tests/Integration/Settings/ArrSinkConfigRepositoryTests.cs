using Contracts.Settings;
using DB.Data.Config;
using Domain.Wantlist.Arr;
using Harness;

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
			ApiKey = Some.Of(new Secret(RadarrKey)),
			QualityProfileId = Some.Of(4),
			RootFolderPath = Some.Of("/movies"),
			Monitored = true,
			SearchOnAdd = false,
			DeleteFiles = true,
		},
		new ArrInstanceConfig
		{
			Url = Some.Of("http://sonarr:8989"),
			ApiKey = Some.Of(new Secret(SonarrKey)),
			QualityProfileId = Some.Of(6),
			RootFolderPath = Some.Of("/tv"),
			AnimeQualityProfileId = Some.Of(8),
			AnimeRootFolderPath = Some.Of("/anime"),
			Monitored = false,
			SearchOnAdd = true,
			DeleteFiles = false,
		},
		ArrTagScheme.Default with
		{
			Prefix = "media",
			FeedSource = ArrFeedTagSource.Id,
			PlainPrefix = true,
			Protection = "keep",
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
}
