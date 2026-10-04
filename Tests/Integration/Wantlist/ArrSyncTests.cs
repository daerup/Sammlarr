using System.Net;
using System.Text;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Sync;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Wantlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ArrSyncTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Radarr = "radarr.test";

	private static readonly MediaSpec Inception = new("Inception", MediaType.Movie, [Tmdb("27205")]);

	private static readonly WantlistSyncTrigger Trigger = new(new ManualTrigger());

	private static readonly ArrInstanceConfig Instance = ArrInstanceConfig.Empty with
	{
		Url = Some.Of($"http://{Radarr}"),
		ApiKey = Some.Of("0123456789abcdef0123456789abcdef"),
		QualityProfileId = Some.Of(4),
		RootFolderPath = Some.Of("/movies"),
	};

	private readonly List<string> _added = [];

	private string _titles = "[]";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Before(Test)]
	public async Task Background()
	{
		var sonarr = Instance with { Url = Some.Of("http://sonarr.test") };
		await host.Resolve<IArrSinkConfigService>().StoreAsync(new ArrSinkConfig(Instance, sonarr, ArrTagScheme.Default));
		host.HttpStub.RespondWithJsonForUrl("sonarr.test/api/v3/series", "[]");
		host.HttpStub.RespondWithJsonForUrl("sonarr.test/api/v3/tag", "[]");
		var config = await host.Resolve<IProviderConfigService>().GetAsync();
		await host.Resolve<IProviderConfigService>().StoreAsync(config with { Sink = SinkKind.Arr });
		host.HttpStub.RespondWithJsonForUrl($"{Radarr}/api/v3/movie/lookup", """[{"title":"Inception","tmdbId":27205}]""");
		host.HttpStub.RespondForUrl($"{Radarr}/api/v3/movie", Movies);
		host.HttpStub.RespondForUrl(
			$"{Radarr}/api/v3/tag",
			request => Json(request.Method == HttpMethod.Post ? """{"id":1,"label":"sammlarr-u-test-user"}""" : """[{"id":1,"label":"sammlarr-u-test-user"}]""")
		);
	}

	[Test]
	public async Task SyncAsync_ForATitleAddedHere_AddsItToRadarrTaggedForItsUser()
	{
		var movie = await host.Movie(Inception).SeedAsync();
		await host.Resolve<IWantlistService>().AddAsync([movie], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)));

		await host.Resolve<IWantlistSyncEngine>().SyncAsync(Trigger);

		await Assert.That(_added.Single()).Contains("\"tags\":[1]");
		await Assert.That(_added.Single()).Contains("\"rootFolderPath\":\"/movies\"");
		await Assert.That((await host.Resolve<IWantlistService>().GetOutboxAsync()).Entries).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_ForATitleTaggedForTheUserInRadarr_PullsItIntoTheWantlist()
	{
		var movie = await host.Movie(Inception).SeedAsync();
		_titles = """[{"id":10,"title":"Inception","year":2010,"tmdbId":27205,"tags":[1]}]""";

		await host.Resolve<IWantlistSyncEngine>().SyncAsync(Trigger);

		await Assert
			.That((await host.Resolve<IWantlistService>().GetAsync()).Find(movie).Map(item => item.Provenance.Value))
			.IsSome()
			.And.IsEqualTo(new SinkOrigin(SinkKind.Arr));
	}

	[Test]
	public async Task SyncAsync_ForAnUntaggedTitleInRadarr_LeavesTheWantlistAlone()
	{
		await host.Movie(Inception).SeedAsync();
		_titles = """[{"id":10,"title":"Inception","year":2010,"tmdbId":27205,"tags":[]}]""";

		await host.Resolve<IWantlistSyncEngine>().SyncAsync(Trigger);

		await Assert.That((await host.Resolve<IWantlistService>().GetAsync()).Ids).IsEmpty();
	}

	private HttpResponseMessage Movies(HttpRequestMessage request)
	{
		if (request.Method != HttpMethod.Post)
		{
			return Json(_titles);
		}

		_added.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
		return Json("""{"id":5,"tmdbId":27205,"tags":[1]}""");
	}

	private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
