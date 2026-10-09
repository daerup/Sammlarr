using System.Diagnostics;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using Harness;
using Observability.Tracing;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunTraceFlowTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IFeedRunEngine Engine => host.Resolve<IFeedRunEngine>();

	[Test]
	public async Task Run_WithStoredFoundAndMissingTitles_SettlesEachTitleOnce()
	{
		var feedId = await SeedFeedAsync("traced-list");
		host.MdblistFeedContains("traced-list", Item("Fight Club", Tmdb("550")), Item("Heat", Tmdb("949")));
		await host.Movie(TestMedia.FightClub).SeedAsync();

		await Engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var snapshot = await SnapshotAsync(feedId);
		await Assert.That(snapshot.Fates.Started).IsEqualTo(2);
		await Assert.That(snapshot.Fates.Matched + snapshot.Fates.NotFound + snapshot.Fates.CouldNotLookUp).IsEqualTo(2);
		await Assert.That(snapshot.Fates.Known).IsEqualTo(1);
		await Assert.That(snapshot.Titles).IsEmpty();
		await Assert.That(snapshot.Ended).IsSome();
	}

	[Test]
	public async Task Run_ThroughAListPage_RecordsThePageAndEndsWithTheRunsCounts()
	{
		var feedId = await SeedFeedAsync("paged-list");
		host.MdblistFeedContains("paged-list", Item("Fight Club", Tmdb("550")), Item("Heat", Tmdb("949")));

		await Engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var snapshot = await SnapshotAsync(feedId);
		await Assert.That(snapshot.Progress.Page).IsSome().And.IsEqualTo(new SourcePage(1, new None()));
		var ended = await Assert.That(snapshot.Ended).IsSome();
		var counts = await Assert.That(ended!.Counts).IsSome();
		await Assert.That(counts.Total).IsEqualTo(2);
	}

	[Test]
	public async Task EnsureSinkIds_InsideARun_RecordsNoTitle()
	{
		var feedId = await SeedFeedAsync("sink-ids-list");
		var heat = await host.Movie(new MediaSpec("Heat", MediaType.Movie, [Tmdb("949")])).SeedAsync();

		using (host.Resolve<IFeedRunTrace>().Start(feedId, host.Resolve<ICurrentUser>().Id))
		{
			await host.Resolve<IMediaService>().EnsureSinkIdsAsync([heat], SinkKind.Plex);
		}

		var snapshot = await SnapshotAsync(feedId);
		await Assert.That(snapshot.Fates.Started).IsEqualTo(0);
		await Assert.That(snapshot.Nodes).IsEmpty();
	}

	[Test]
	public async Task Run_WithinTheHourOfAMiss_RecordsItAsNotFoundEarlier()
	{
		var feedId = await SeedFeedAsync("missed-list");
		host.MdblistFeedContains("missed-list", Item("Heat", Tmdb("949")));
		await Engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var snapshot = await SnapshotAsync(feedId);
		await Assert.That(snapshot.Fates.Earlier).IsEqualTo(1);
		await Assert.That(snapshot.Fates.NotFound).IsEqualTo(1);
	}

	[Test]
	public async Task Run_WithItemsListedWithoutIds_CountsThemAsNotFound()
	{
		var feedId = await SeedFeedAsync("idless-list");
		host.MdblistFeedContains(
			"idless-list",
			Item("Fight Club", Tmdb("550")),
			new FeedItemSpec(null, MediaType.Movie, null, new Dictionary<MediaProvider, string>())
		);
		await host.Movie(TestMedia.FightClub).SeedAsync();

		await Engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var snapshot = await SnapshotAsync(feedId);
		var saved = (await host.Resolve<IFeedRepository>().GetRunsAsync(RunQuery.Take(1) with { Feed = Some.Of(feedId) })).Single();
		await Assert.That(snapshot.Fates.ListedWithoutIds).IsEqualTo(1);
		await Assert.That(snapshot.Fates.Settled).IsEqualTo(saved.Counts.Total);
		await Assert.That(snapshot.Requests.GetValueOr(0) + snapshot.Fates.ListedWithoutIds).IsEqualTo(saved.Counts.Total);
		await Assert.That(snapshot.Fates.NotFound + snapshot.Fates.ListedWithoutIds).IsEqualTo(saved.Counts.UnmatchedNotFound);
	}

	[Test]
	public async Task Run_WithServiceSpansOn_StillTracesEachTitle()
	{
		using var root = new Activity("trace-flow").Start();
		var traceId = root.TraceId;
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == Instrumentation.ServiceName,
			Sample = (ref options) => options.TraceId == traceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
		};
		ActivitySource.AddActivityListener(listener);
		var feedId = await SeedFeedAsync("proxied-list");
		host.MdblistFeedContains("proxied-list", Item("Fight Club", Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();

		await Engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var snapshot = await SnapshotAsync(feedId);
		await Assert.That(snapshot.Fates.Started).IsEqualTo(1);
		await Assert.That(snapshot.Fates.Matched).IsEqualTo(1);
	}

	[Test]
	public async Task Run_WithAPlexMatch_RecordsTheHopThenTheSettle()
	{
		var feedId = await SeedFeedAsync("plex-list");
		host.PlexHolds(TestMedia.Inception);
		await host.PlexSearchReturns("Inception", TestMedia.Inception);
		host.MdblistFeedContains("plex-list", Item("Inception", Tmdb("27205")));

		await Engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var events = await EventsAsync(feedId);
		await Assert.That(events.Select(AskedAt).WhereSome()).Contains(new HopPlace(RemoteService.Plex, TraceStage.Ids));
		await Assert.That(SettledAs(events[^1])).IsSome().And.IsEqualTo(TitleFate.Matched);
	}

	[Test]
	public async Task Run_WithTwoTitlesAtOnce_ParentsEachHopToItsOwnTitle()
	{
		var feedId = await SeedFeedAsync("pair-list");
		host.PlexHolds(TestMedia.Inception);
		host.PlexHolds(TestMedia.Interstellar);
		await host.PlexSearchReturns("Inception", TestMedia.Inception);
		await host.PlexSearchReturns("Interstellar", TestMedia.Interstellar);
		host.MdblistFeedContains("pair-list", Item("Inception", Tmdb("27205")), Item("Interstellar", Tmdb("157336")));

		await Engine.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var steps = await StepsAsync(feedId);
		var asked = steps
			.Where(step => AskedAt(step.Event).Match(place => place.Service == RemoteService.Plex, () => false))
			.Select(step => step.Title)
			.WhereSome();
		await Assert.That(asked.Distinct()).IsEquivalentTo([0, 1]);
		await Assert.That((await SnapshotAsync(feedId)).Fates.Matched).IsEqualTo(2);
	}

	private async Task<IReadOnlyList<TraceStep>> StepsAsync(FeedId feedId)
	{
		var from = (await SnapshotAsync(feedId)).Cursor with { Seq = 0 };
		var read = await host.Resolve<IFeedRunTraces>().ReadAsync(feedId, from);
		return (await Assert.That(read).IsOfUnionType<TraceRead, TraceBatch>())!.Steps;
	}

	private async Task<IReadOnlyList<TraceEvent>> EventsAsync(FeedId feedId) => [.. (await StepsAsync(feedId)).Select(step => step.Event)];

	private static Optional<HopPlace> AskedAt(TraceEvent traceEvent) =>
		traceEvent switch
		{
			HopAsked asked => Some.Of(new HopPlace(asked.Service, asked.Stage)),
			_ => new None(),
		};

	private static Optional<TitleFate> SettledAs(TraceEvent traceEvent) =>
		traceEvent switch
		{
			TitleSettled settled => Some.Of(settled.Fate),
			_ => new None(),
		};

	private async Task<TraceSnapshot> SnapshotAsync(FeedId feedId)
	{
		var read = await host.Resolve<IFeedRunTraces>().ReadAsync(feedId, TraceCursor.Start);
		return (await Assert.That(read).IsOfUnionType<TraceRead, TraceSnapshot>())!;
	}

	private static FeedItemSpec Item(string title, params ProviderId[] ids) =>
		new(title, MediaType.Movie, null, ids.ToDictionary(id => id.Provider, id => id.Value));

	private async Task<FeedId> SeedFeedAsync(string identifier)
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var entity = new FeedRow
		{
			Name = "Traced",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
