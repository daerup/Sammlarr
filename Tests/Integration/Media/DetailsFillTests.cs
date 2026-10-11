using System.Net;
using API.Mdblist;
using API.Tmdb;
using Contracts;
using Contracts.Jobs;
using Contracts.Media;
using Core.Jobs;
using Core.Media;
using Domain.Jobs;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class DetailsFillTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForAGapANewServiceFills_FillsItAndRestsItTwoWeeks()
	{
		await host.WithTmdbAccount(SammlarrTestHost.TmdbApiKey);
		host.Movie(TestMedia.FightClub).ServedByTmdb(title: "Fight Club", genres: ["Drama"]);
		var fightClub = await WantlistedAsync(TestMedia.FightClub, Complete("Fight Club") with { Genres = [] });

		await FillAsync();
		host.SetCurrentTime(Now.AddDays(13));
		await FillAsync();

		var stored = await StoredAsync(fightClub);
		await Assert.That(stored.Details.Genres).Contains("Drama");
		await Assert.That(stored.Answers.Single(answer => answer.Provider == RemoteService.Tmdb).AskableAt).IsEqualTo(Now.AddDays(14));
		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(1);
	}

	[Test]
	public async Task RunAsync_AfterAFailure_WaitsOutTheBackoff()
	{
		await host.WithTmdbAccount(SammlarrTestHost.TmdbApiKey);
		host.TmdbDetailFails(TestMedia.FightClub, HttpStatusCode.ServiceUnavailable);
		await WantlistedAsync(TestMedia.FightClub, Complete("Fight Club") with { Genres = [] });

		await FillAsync();
		host.SetCurrentTime(Now.AddMinutes(30));
		await FillAsync();
		var withinTheBackoff = host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub));
		host.SetCurrentTime(Now.AddHours(2));
		await FillAsync();

		await Assert.That(withinTheBackoff).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(2);
	}

	[Test]
	public async Task RunAsync_ForATitleNoListHolds_LeavesItAlone()
	{
		await host.WithTmdbAccount(SammlarrTestHost.TmdbApiKey);
		host.Movie(TestMedia.FightClub).ServedByTmdb(title: "Fight Club", genres: ["Drama"]);
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		await StoreAsync(fightClub, Complete("Fight Club") with { Genres = [] });

		await FillAsync();

		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(0);
		await Assert.That((await StoredAsync(fightClub)).Details.Genres).IsEmpty();
	}

	[Test]
	public async Task RunAsync_WhenTheOnlyServiceIsBlockedPastFiveSeconds_LeavesTheTitleAndRecordsNothing()
	{
		await host.WithTmdbAccount(SammlarrTestHost.TmdbApiKey);
		host.Resolve<TmdbOptions>().Http.EnrichWaitSeconds = 15;
		host.Movie(TestMedia.FightClub).ServedByTmdb(title: "Fight Club", genres: ["Drama"]);
		var fightClub = await WantlistedAsync(TestMedia.FightClub, Complete("Fight Club") with { Genres = [] });
		await host.RateLimitedAsync(RemoteService.Tmdb, TimeSpan.FromSeconds(10));

		await FillAsync();

		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(0);
		await Assert.That((await StoredAsync(fightClub)).Answers).IsEmpty();
	}

	[Test]
	public async Task RunAsync_AtTheBackgroundFloor_LeavesTheTitlesWithoutABackoff()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var fightClub = await WantlistedAsync(TestMedia.FightClub, Complete("Fight Club"));
		var budget = host.Resolve<MdblistBudget>();
		budget.Record(SammlarrTestHost.MdblistApiKey, Allowance(remaining: 30, reset: Now.AddHours(-1)));
		budget.Record(SammlarrTestHost.MdblistApiKey, Allowance(remaining: 1000, reset: Now.AddDays(1)));
		budget.Record(SammlarrTestHost.MdblistApiKey, Allowance(remaining: 400, reset: Now.AddDays(1)));

		await FillAsync();

		await Assert.That(host.HttpStub.Requests($"{TestUrls.MdblistApi}/tmdb/movie")).IsEqualTo(0);
		await Assert.That((await StoredAsync(fightClub)).Answers).IsEmpty();
	}

	[Test]
	public async Task RunAsync_AboveTheBackgroundFloor_AsksMdblist()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		await WantlistedAsync(TestMedia.FightClub, Complete("Fight Club"));
		var budget = host.Resolve<MdblistBudget>();
		budget.Record(SammlarrTestHost.MdblistApiKey, Allowance(remaining: 30, reset: Now.AddHours(-1)));
		budget.Record(SammlarrTestHost.MdblistApiKey, Allowance(remaining: 1000, reset: Now.AddDays(1)));
		budget.Record(SammlarrTestHost.MdblistApiKey, Allowance(remaining: 600, reset: Now.AddDays(1)));

		await FillAsync();

		await Assert.That(host.HttpStub.Requests($"{TestUrls.MdblistApi}/tmdb/movie")).IsEqualTo(1);
	}

	private static System.Net.Http.Headers.HttpResponseHeaders Allowance(long remaining, DateTimeOffset reset)
	{
		var answered = new HttpResponseMessage(HttpStatusCode.OK);
		answered.Headers.Add("x-ratelimit-remaining", $"{remaining}");
		answered.Headers.Add("x-ratelimit-reset", $"{reset.ToUnixTimeSeconds()}");
		return answered.Headers;
	}

	[Test]
	public async Task RunAsync_ForTwoOwners_EachJobUsesItsOwnersKeys()
	{
		await host.SignInAs("Ann");
		await host.WithTmdbAccount("ann-tmdb-key");
		host.Movie(TestMedia.FightClub).ServedByTmdb(title: "Fight Club", genres: ["Drama"]);
		await WantlistedAsync(TestMedia.FightClub, Complete("Fight Club") with { Genres = [] });
		await host.SignInAs("Ben");
		await host.WithTmdbAccount("ben-tmdb-key");
		host.Movie(TestMedia.Inception).ServedByTmdb(title: "Inception", genres: ["Thriller"]);
		await WantlistedAsync(TestMedia.Inception, Complete("Inception") with { Genres = [] });
		var due = Now + host.Resolve<SchedulerOptions>().FillDetailsGrace;
		host.SetCurrentTime(due);
		await host.Resolve<IJobRepository>().ReleaseDueAsync(due);

		while (await RunNextJobAsync()) { }

		await Assert.That(host.HttpStub.Requests("api_key=ann-tmdb-key")).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests("api_key=ben-tmdb-key")).IsEqualTo(1);
	}

	private static MovieDetails Complete(string title)
	{
		return new MovieDetails
		{
			Title = title,
			Tagline = "Tagline",
			Summary = "An overview",
			PosterUrl = "/poster.jpg",
			BackdropUrl = "/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 15),
			Genres = ["Drama"],
			Runtime = TimeSpan.FromMinutes(120),
			Ratings = MediaRatings.From([new TmdbRating(8f, Some.Of(500))]),
		};
	}

	private async Task<MediaId> WantlistedAsync(MediaSpec spec, MediaDetails details)
	{
		var id = await host.Movie(spec).OnWantlist(new ManualOrigin(ManualSource.Search));
		await StoreAsync(id, details);
		return id;
	}

	private async Task StoreAsync(MediaId id, MediaDetails details)
	{
		await host.Resolve<IMediaRepository>().SaveDetailsAsync([new DetailsChange(id, new None(), new CachedDetails(details, Now, Now, []))]);
	}

	private async Task<CachedDetails> StoredAsync(MediaId id)
	{
		var stored = (await host.Resolve<IMediaRepository>().GetAsync([id])).Single();
		var cached = await Assert.That(stored.Cached).IsSome();
		return cached!;
	}

	private async Task FillAsync()
	{
		await using var scope = host.Resolve<IServiceScopeFactory>().CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<DetailsFill>().RunAsync(CancellationToken.None);
	}

	private async Task<bool> RunNextJobAsync()
	{
		return await new JobRunner(
			host.Resolve<IServiceScopeFactory>(),
			host.Resolve<JobSignal>(),
			host.Resolve<JobAlarm>(),
			host.Resolve<JobRunnerStatus>(),
			host.Resolve<IDateTimeProvider>(),
			NullLogger<JobRunner>.Instance
		).TryRunNextAsync(CancellationToken.None);
	}

	private static string TmdbDetail(MediaSpec spec) => string.Format(TmdbEndpoints.Detail, TmdbEndpoints.MovieType, spec.Ids[0].Value);
}
