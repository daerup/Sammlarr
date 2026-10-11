using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;
using API.Mdblist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Media;
using DB;
using DB.Data.Feed;
using DB.Data.Media;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class CachedRunBenchmark(SammlarrTestHost host) : IAsyncDisposable
{
	private const int Titles = 4000;
	private const int PageSize = 1000;
	private const string List = "bench/cached-list";

	private static readonly RatingSource[] Sources =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
		RatingSource.Metacritic,
		RatingSource.Trakt,
	];

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	[Explicit]
	public async Task RunAsync_ForAFullyCachedList_ReportsItsMeasures()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		await SeedLibraryAsync();
		StubList();
		var feedId = await SeedFeedAsync();
		var owning = await host.Resolve<IFeedRunEngine>().RunAsync(feedId, new FeedRunTrigger(new Manual()));
		var owned = await Assert.That(owning).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(owned!.Delta.Adds).Count().IsEqualTo(Titles);
		host.SetCurrentTime(new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero));
		await using var scope = host.Resolve<IServiceScopeFactory>().CreateAsyncScope();
		var engine = scope.ServiceProvider.GetRequiredService<IFeedRunEngine>();
		var before = host.HttpStub.Requests("http");

		var clock = Stopwatch.StartNew();
		var result = await engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));
		clock.Stop();

		var calls = host.HttpStub.Requests("http") - before;
		var recording = await RecordAgainAsync(feedId);
		Console.WriteLine($"cached 4k run: {clock.ElapsedMilliseconds} ms, recording: {recording} ms, remote calls: {calls}");
		var kept = await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(kept!.Delta.Keeps).Count().IsEqualTo(Titles);
	}

	private async Task<long> RecordAgainAsync(FeedId feedId)
	{
		var feeds = host.Resolve<IFeedRepository>();
		var latest = (await feeds.GetRunsAsync(RunQuery.Take(1) with { Feed = Some.Of(feedId) })).Single();
		var copy = latest with { Id = RunId.From(Guid.NewGuid()) };
		var clock = Stopwatch.StartNew();
		await feeds.StartRunAsync(copy);
		await feeds.CompleteRunAsync(copy, Some.Of(new AbsenceChanges(new HashSet<MediaId>())));
		return clock.ElapsedMilliseconds;
	}

	private async Task SeedLibraryAsync()
	{
		var enrichers = host.Resolve<IKeyedServiceSet<RemoteService, IDetailsEnricher>>();
		var now = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
		await using var context = await host.Resolve<IDbContextFactory<SammlarrDbContext>>().CreateDbContextAsync();
		context.ChangeTracker.AutoDetectChangesEnabled = false;
		foreach (var index in Enumerable.Range(0, Titles))
		{
			var media = new MediaRow { Id = Guid.CreateVersion7() };
			foreach (var external in Keys(index))
			{
				media.ExternalIds.Add(external.ToExternalIdEntity(media));
			}

			var details = new MovieDetailsRow
			{
				MediaId = media.Id,
				Title = $"Title {index}",
				Tagline = "Tagline",
				Summary = "An overview",
				PosterUrl = "/poster.jpg",
				BackdropUrl = "/backdrop.jpg",
				ReleaseDate = new DateOnly(2000 + (index % 20), 1, 15),
				Genres = ["Action", "Drama"],
				Runtime = TimeSpan.FromMinutes(100 + (index % 60)),
				RefreshedAt = now.AddDays(-(index % 10)),
				AskedAt = now.AddDays(-(index % 10)),
				Ratings =
				[
					.. Sources.Select(source => new MediaRatingRow
					{
						Source = source,
						NormalizedValue = 5 + (index % 50) / 10f,
						VoteCount = 1000 + index,
					}),
				],
				PageLinks =
				[
					new MediaPageLinkRow { Site = MediaProvider.RottenTomatoes, Path = $"m/title_{index}" },
					new MediaPageLinkRow { Site = MediaProvider.Metacritic, Path = $"title-{index}" },
				],
			};
			var seeded = new MediaDetailsFactory().ToCached(details).Details;
			details.Answers =
			[
				.. enrichers
					.Keys.Select(remote => enrichers.Find(remote).Map(enricher => ProviderAnswer.After(remote, enricher.Fills, seeded, details.RefreshedAt)))
					.WhereSome()
					.Select(answer => new ProviderAnswerRow
					{
						Provider = answer.Provider,
						Unfilled = answer.Unfilled,
						AnsweredAt = answer.AnsweredAt,
					}),
			];
			media.Details = details;
			context.Media.Add(media);
		}

		await context.SaveChangesAsync();
	}

	private static IReadOnlyList<IIdentityKey> Keys(int index) =>
		[
			MdblistId.From($"mdb{index}", MediaType.Movie),
			TmdbId.From($"{100000 + index}", MediaType.Movie),
			ImdbId.From($"tt{index:D7}", MediaType.Movie),
			TraktId.From($"{500000 + index}", MediaType.Movie),
			TvdbId.From($"{700000 + index}", MediaType.Movie),
			PlexId.From($"plex{index}", MediaType.Movie),
		];

	private async Task<FeedId> SeedFeedAsync()
	{
		var entity = new FeedRow
		{
			Name = List,
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new Mirror(3),
			Identifier = List,
		};
		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}

	private void StubList()
	{
		host.HttpStub.RespondForUrl(
			MdblistLists.ItemsPath(List),
			request =>
			{
				var offset = int.Parse(HttpUtility.ParseQueryString(request.RequestUri?.Query ?? string.Empty)["offset"] ?? "0");
				var take = Math.Max(Math.Min(PageSize, Titles - offset), 0);
				var json = new StringBuilder("{\"movies\":[");
				json.AppendJoin(',', Enumerable.Range(offset, take).Select(ItemJson));
				json.Append("],\"shows\":[]}");
				var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json.ToString(), Encoding.UTF8, "application/json") };
				if (offset + PageSize < Titles)
				{
					response.Headers.TryAddWithoutValidation("X-Has-More", "true");
				}

				return response;
			}
		);
	}

	private static string ItemJson(int index)
	{
		var tmdb = 100000 + index;
		var trakt = 500000 + index;
		var year = 2000 + (index % 20);
		return $$"""
			{"id":{{index + 1}},"ids":{"imdb":"tt{{index:D7}}","tmdb":{{tmdb}},"tvdb":null,"mdblist":"mdb{{index}}","mal":null,"trakt":{{trakt}}},"rank":{{index
				+ 1}},"title":"Title {{index}}","status":"Released","country":"US","runtime":120,"mediatype":"movie","release_date":"{{year}}-01-15","release_year":{{year}},"spoken_language":"English"}
			""";
	}
}
