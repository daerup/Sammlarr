using System.Globalization;
using System.Net;
using System.Text;
using API.Mdblist;
using Contracts.Feed;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MdblistBatchingTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string TitleBatches = $"{TestUrls.MdblistApi}/tmdb/movie";

	private static readonly FieldNeeds CardRatings = new(MediaFields.None, MediaFields.AllRatings);

	private static readonly FieldNeeds CardSummaryAndRatings = new(MediaFields.None, MediaField.Summary | MediaFields.AllRatings);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	[Arguments(250, 2)]
	public async Task GetFeedItemsAsync_ForAPageOfNewTitles_SendsFullBatches(int titles, int batches)
	{
		await ConnectAsync();
		host.HttpStub.RespondWithJsonForUrl(TitleBatches, "[]");
		host.MdblistFeedContains("new-list", [.. Titles(0, titles)]);

		await LoadAsync("new-list", CardRatings);

		await Assert.That(host.HttpStub.Requests(TitleBatches)).IsEqualTo(batches);
	}

	[Test]
	public async Task GetFeedItemsAsync_For1200NewTitles_SendsTwoPagesAndSixBatches()
	{
		await ConnectAsync();
		host.HttpStub.RespondWithJsonForUrl(TitleBatches, "[]");
		host.MdblistFeedPages("long-list", [.. Titles(0, 1000)], [.. Titles(1000, 200)]);

		await LoadAsync("long-list", CardRatings);

		await Assert.That(host.HttpStub.Requests(MdblistLists.ItemsPath("long-list"))).IsEqualTo(2);
		await Assert.That(host.HttpStub.Requests(TitleBatches)).IsEqualTo(6);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnUnreadableAnswer_AsksNoTitleAgainOnItsOwn()
	{
		await ConnectAsync();
		host.HttpStub.RespondForUrl(TitleBatches, _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>maintenance</html>") });
		host.MdblistFeedContains("new-list", [.. Titles(0, 250)]);

		await LoadAsync("new-list", CardRatings);

		await Assert.That(host.HttpStub.Requests(TitleBatches)).IsGreaterThanOrEqualTo(1).And.IsLessThanOrEqualTo(2);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForNewTitlesWhoseTmdbStepAnswersFirst_SendsFullBatches()
	{
		await ConnectAsync();
		await host.WithTmdbAccount(SammlarrTestHost.TmdbApiKey);
		host.HttpStub.RespondForUrl(
			$"{TestUrls.TmdbApi}/movie",
			request => Json(TmdbDetail(int.Parse(request.RequestUri!.Segments[^1], CultureInfo.InvariantCulture)))
		);
		host.HttpStub.RespondWithJsonForUrl(TitleBatches, $"[{string.Join(",", Enumerable.Range(0, 1000).Select(MdblistTitle))}]");
		host.MdblistFeedContains("new-list", [.. Titles(0, 1000)]);

		await LoadAsync("new-list", CardSummaryAndRatings);

		await Assert.That(host.HttpStub.Requests($"{TestUrls.TmdbApi}/movie")).IsEqualTo(1000);
		await Assert.That(host.HttpStub.Requests(TitleBatches)).IsEqualTo(5);
		await Assert.That(host.HttpStub.Requests($"{TestUrls.MdblistApi}/imdb/movie")).IsEqualTo(0);
	}

	private async Task ConnectAsync()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
	}

	private async Task LoadAsync(string identifier, FieldNeeds needs)
	{
		await using var scope = host.Resolve<IServiceScopeFactory>().CreateAsyncScope();
		var feed = scope.ServiceProvider.GetRequiredKeyedService<IFeedProvider>(FeedProvider.Mdblist);
		await feed.GetFeedItemsAsync(identifier, needs).InListOrderAsync().ToListAsync();
	}

	private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	private static string TmdbDetail(int tmdbId)
	{
		return $$$"""{"id":{{{tmdbId}}},"title":"Title {{{tmdbId - 100000}}}","release_date":"2024-01-15","overview":"A summary","external_ids":{"imdb_id":"tt0{{{tmdbId}}}","tvdb_id":null}}""";
	}

	private static string MdblistTitle(int index)
	{
		return $$$"""{"title":"Title {{{index}}}","year":2024,"ids":{"imdb":"tt0{{{100000 + index}}}","tmdb":{{{100000 + index}}}},"ratings":[]}""";
	}

	private static IEnumerable<FeedItemSpec> Titles(int from, int count)
	{
		return Enumerable
			.Range(from, count)
			.Select(index => new FeedItemSpec(
				$"Title {index}",
				MediaType.Movie,
				null,
				new Dictionary<MediaProvider, string> { [MediaProvider.Tmdb] = $"{100000 + index}" }
			));
	}
}
