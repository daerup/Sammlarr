using Contracts.Blocklist;
using Contracts.Media;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using DB.Data.Feed;
using DB.Data.Feed.Run;
using DB.Data.Media;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Harness;
using Integration.Tests.Housekeeping;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaMergeTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private IMediaMerge Testee => host.Resolve<IMediaMerge>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task MergeAsync_ForTwoRowsOfOneTitle_KeepsTheOlderRowWithEveryId()
	{
		var (older, newer) = await PairAsync();

		var merged = await Testee.MergeAsync(newer, older);

		await Assert.That(merged).IsOfUnionType<MergeOutcome, Merged>().And.IsEqualTo(new Merged(older, newer));
		var stored = (await host.Resolve<IMediaRepository>().GetAsync([older, newer])).Single();
		await Assert.That(stored.Identity).IsEqualTo(new MediaIdentity(older, [Tmdb("15739"), Imdb("tt0083564")]));
	}

	[Test]
	public async Task MergeAsync_WhenOnlyTheNewerRowIsWanted_KeepsTheNewerRow()
	{
		var (older, newer) = await PairAsync();
		await host.Resolve<IWantlistRepository>().AddAsync([newer], new ManualOrigin(ManualSource.Search));

		var merged = await Testee.MergeAsync(older, newer);

		await Assert.That(merged).IsOfUnionType<MergeOutcome, Merged>().And.IsEqualTo(new Merged(newer, older));
	}

	[Test]
	public async Task MergeAsync_WhenOneUserWantsBothRows_KeepsTheOlderEntryOnTheSurvivor()
	{
		var (older, newer) = await PairAsync();
		var wantlist = host.Resolve<IWantlistRepository>();
		await wantlist.AddAsync([newer], new ManualOrigin(ManualSource.Search));
		await wantlist.AddAsync([older], new ManualOrigin(ManualSource.Browse));
		await WantedAsync(newer, Now.AddHours(2));
		await WantedAsync(older, Now.AddHours(3));

		await Testee.MergeAsync(older, newer);

		var wanted = (await wantlist.GetAllAsync()).Single();
		await Assert.That(wanted.MediaId).IsEqualTo(older);
		await Assert.That(wanted.Provenance).IsEqualTo(new ManualOrigin(ManualSource.Search));
	}

	[Test]
	public async Task MergeAsync_ForAnotherUsersWantlistEntryAndOutboxRow_MovesThemToTheSurvivor()
	{
		var (older, newer) = await PairAsync();
		var guest = await host.SeedLocalUser("guest", "a test password", UserRole.Member);
		await host.Resolve<IWantlistRepository>().AddAsync([older], new ManualOrigin(ManualSource.Search));
		host.CurrentUser.Id = guest;
		await host.Resolve<IWantlistRepository>().AddAsync([newer], new ManualOrigin(ManualSource.Browse));
		await host.Resolve<IWantlistOutbox>().EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [newer]);
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededAdmin.Id);

		await Testee.MergeAsync(older, newer);

		host.CurrentUser.Id = guest;
		await Assert.That((await host.Resolve<IWantlistRepository>().GetAllAsync()).Single().MediaId).IsEqualTo(older);
		await Assert.That((await host.Resolve<IWantlistOutbox>().GetAllAsync(SinkKind.Plex)).Single().MediaId).IsEqualTo(older);
	}

	[Test]
	public async Task MergeAsync_ForAbsences_KeepsTheLowerOfTwoAndDropsOneTheOtherRowWasSeenWithout()
	{
		var (older, newer) = await PairAsync();
		var both = await host.SeedFeedAsync();
		var one = await host.SeedFeedAsync();
		await using (var ctx = await host.ContextFactory.CreateDbContextAsync())
		{
			ctx.FeedAbsences.AddRange(Absence(both, older, 3), Absence(both, newer, 1), Absence(one, newer, 2));
			await ctx.SaveChangesAsync();
		}

		await Testee.MergeAsync(older, newer);

		await using var read = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(await read.FeedAbsences.IgnoreQueryFilters().SingleAsync()).IsEqualTo(Absence(both, older, 1));
	}

	[Test]
	public async Task MergeAsync_ForItemsOfOneRun_KeepsOneItemPerRun()
	{
		var (older, newer) = await PairAsync();
		var feed = await host.SeedFeedAsync();
		var both = await host.SeedRunAsync(feed, Now, older.Value);
		var later = await host.SeedRunAsync(feed, Now.AddDays(1), newer.Value);
		await using (var ctx = await host.ContextFactory.CreateDbContextAsync())
		{
			ctx.FeedRunItems.Add(
				new FeedRunItemRow
				{
					Id = Guid.NewGuid(),
					FeedRunId = both,
					MediaId = newer.Value,
					State = FeedRunItemState.Added,
				}
			);
			await ctx.SaveChangesAsync();
		}

		await Testee.MergeAsync(older, newer);

		await using var read = await host.ContextFactory.CreateDbContextAsync();
		var items = await read.FeedRunItems.IgnoreQueryFilters().ToListAsync();
		await Assert.That(items.Select(item => (item.FeedRunId, item.MediaId))).IsEquivalentTo([(both, older.Value), (later, older.Value)]);
	}

	[Test]
	public async Task MergeAsync_WhenBothRowsHaveDetails_KeepsTheSurvivorsAndFillsWhatItLacks()
	{
		var older = await host.Movie(Spec("Annie (1982)", Ids.Tmdb("15739")))
			.SeedWithDetailsAsync("Annie (1982)", Now, poster: "", answeredBy: [RemoteService.Tmdb]);
		var newer = await host.Movie(Spec("Annie", Ids.Imdb("tt0083564")))
			.SeedWithDetailsAsync("Annie", Now.AddDays(-2), poster: "/annie.jpg", answeredBy: [RemoteService.Tmdb, RemoteService.Mdblist]);
		await AgeAsync(older, newer);
		await DetailedAsync(
			older,
			details =>
			{
				((MovieDetailsRow)details).Runtime = null;
				details.Ratings[0].NormalizedValue = 6f;
				details.PageLinks.Add(new MediaPageLinkRow { Site = MediaProvider.Letterboxd, Path = "/film/annie" });
			}
		);
		await DetailedAsync(
			newer,
			details =>
			{
				details.Ratings.Add(new MediaRatingRow { Source = RatingSource.Imdb, NormalizedValue = 8f });
				details.PageLinks.Add(new MediaPageLinkRow { Site = MediaProvider.Letterboxd, Path = "/film/annie-1999" });
				details.PageLinks.Add(new MediaPageLinkRow { Site = MediaProvider.RottenTomatoes, Path = "/m/annie" });
			}
		);

		await Testee.MergeAsync(older, newer);

		await using var read = await host.ContextFactory.CreateDbContextAsync();
		var merged = (MovieDetailsRow)await read.MediaDetails.Include(row => row.Ratings).Include(row => row.PageLinks).Include(row => row.Answers).SingleAsync();
		await Assert.That((merged.MediaId, merged.Title, merged.PosterUrl)).IsEqualTo((older.Value, "Annie (1982)", "/annie.jpg"));
		await Assert.That((merged.Runtime, merged.RefreshedAt)).IsEqualTo((TimeSpan.FromHours(2), Now.AddDays(-2)));
		await Assert
			.That(merged.Ratings.Select(rating => (rating.Source, rating.NormalizedValue)))
			.IsEquivalentTo([(RatingSource.Tmdb, 6f), (RatingSource.Imdb, 8f)]);
		await Assert
			.That(merged.PageLinks.Select(link => (link.Site, link.Path)))
			.IsEquivalentTo([(MediaProvider.Letterboxd, "/film/annie"), (MediaProvider.RottenTomatoes, "/m/annie")]);
		await Assert
			.That(merged.Answers.Select(answer => (answer.Provider, answer.AnsweredAt)))
			.IsEquivalentTo([(RemoteService.Tmdb, Now), (RemoteService.Mdblist, Now.AddDays(-2))]);
	}

	[Test]
	public async Task MergeAsync_WhenOnlyTheNewerRowIsBlocked_KeepsTheNewerRowAndItsEntry()
	{
		var (older, newer) = await PairAsync();
		await host.Resolve<IBlocklistRepository>().AddAsync([newer]);

		var merged = await Testee.MergeAsync(older, newer);

		await Assert.That(merged).IsOfUnionType<MergeOutcome, Merged>().And.IsEqualTo(new Merged(newer, older));
		await Assert.That((await host.Resolve<IBlocklistRepository>().GetAllAsync()).Single().MediaId).IsEqualTo(newer);
	}

	[Test]
	public async Task MergeAsync_WhenOneUserQueuedAnAddAndARemove_KeepsTheOneTheirMergedWantlistAgreesWith()
	{
		var (older, newer) = await PairAsync();
		var guest = await host.SeedLocalUser("guest", "a test password", UserRole.Member);
		await host.Resolve<IWantlistRepository>().AddAsync([newer], new ManualOrigin(ManualSource.Search));
		var outbox = host.Resolve<IWantlistOutbox>();
		await outbox.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [newer]);
		await outbox.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Remove, [older]);
		host.CurrentUser.Id = guest;
		await host.Resolve<IWantlistRepository>().AddAsync([older], new ManualOrigin(ManualSource.Browse));
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededAdmin.Id);

		await Testee.MergeAsync(older, newer);

		var queued = (await outbox.GetAllAsync(SinkKind.Plex)).Single();
		await Assert.That((queued.MediaId, queued.Action)).IsEqualTo((older, WantlistOperationAction.Add));
	}

	[Test]
	public async Task MergeAsync_ForRowsQueuedForTwoSinks_KeepsOneRowPerSink()
	{
		var (older, newer) = await PairAsync();
		var outbox = host.Resolve<IWantlistOutbox>();
		await outbox.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [older, newer]);
		await outbox.EnqueueAsync(SinkKind.Arr, WantlistOperationAction.Add, [newer]);

		await Testee.MergeAsync(older, newer);

		await Assert.That((await outbox.GetAllAsync(SinkKind.Plex)).Single().MediaId).IsEqualTo(older);
		await Assert.That((await outbox.GetAllAsync(SinkKind.Arr)).Single().MediaId).IsEqualTo(older);
	}

	[Test]
	public async Task MergeAsync_WhenOnlyTheOtherRowHasDetails_MovesThemToTheSurvivor()
	{
		var older = await host.SeedMediaAsync(Tmdb("15739"));
		var newer = await host.Movie(Spec("Annie", Ids.Imdb("tt0083564"))).SeedWithDetailsAsync("Annie", Now);
		await AgeAsync(older, newer);

		await Testee.MergeAsync(older, newer);

		await using var read = await host.ContextFactory.CreateDbContextAsync();
		var details = await read.MediaDetails.Include(row => row.Ratings).SingleAsync();
		await Assert.That(details.MediaId).IsEqualTo(older.Value);
		await Assert.That(details.Title).IsEqualTo("Annie");
		await Assert.That(details.Ratings).HasSingleItem();
	}

	[Test]
	public async Task MergeAsync_ForNotesOnTheRemovedRow_RemovesThemAndKeepsTheSurvivorsOwn()
	{
		var (older, newer) = await PairAsync();
		var third = await host.SeedMediaAsync(TraktId.From("9600", MediaType.Movie));
		var claims = host.Resolve<IIdentityClaims>();
		await claims.ClaimAsync(new IdentityClaim([Tmdb("15739"), Imdb("tt0083564")], new None(), [RemoteService.Trakt]));
		await claims.ClaimAsync(new IdentityClaim([Imdb("tt0083564"), TraktId.From("9600", MediaType.Movie)], new None(), [RemoteService.Mdblist]));
		await claims.ClaimAsync(new IdentityClaim([TraktId.From("9600", MediaType.Movie)], Some.Of(older), [RemoteService.Mdblist]));
		var noted = await host.Resolve<IIdentityConflicts>().AllAsync();

		await Testee.MergeAsync(older, newer);

		var left = (await host.Resolve<IIdentityConflicts>().AllAsync()).Single();
		await Assert.That(noted).Count().IsEqualTo(3);
		await Assert.That((left.Held, left.Other)).IsEqualTo((older, third));
	}

	[Test]
	public async Task MergeAsync_WhenBothRowsHoldATmdbId_RefusesAndChangesNothing()
	{
		var older = await host.SeedMediaAsync(Tmdb("15739"));
		var newer = await host.SeedMediaAsync(Tmdb("15740"), Imdb("tt0083564"));

		var merged = await Testee.MergeAsync(older, newer);

		await Assert.That(merged).IsOfUnionType<MergeOutcome, Contradicted>().And.IsEqualTo(new Contradicted(new Contradiction(MediaProvider.Tmdb)));
		var stored = await host.Resolve<IMediaRepository>().GetAsync([older, newer]);
		await Assert.That(stored.Single(title => title.Identity.Id == older).Identity).IsEqualTo(new MediaIdentity(older, [Tmdb("15739")]));
		await Assert.That(stored.Single(title => title.Identity.Id == newer).Identity).IsEqualTo(new MediaIdentity(newer, [Tmdb("15740"), Imdb("tt0083564")]));
	}

	[Test]
	public async Task MergeAsync_ForAPairMergedBefore_SaysItIsNoLongerStored()
	{
		var (older, newer) = await PairAsync();
		await Testee.MergeAsync(older, newer);

		var again = await Testee.MergeAsync(older, newer);

		await Assert.That(again).IsOfUnionType<MergeOutcome, NotStored>();
	}

	private async Task<(MediaId Older, MediaId Newer)> PairAsync()
	{
		var older = await host.SeedMediaAsync(Tmdb("15739"));
		var newer = await host.SeedMediaAsync(Imdb("tt0083564"));
		await AgeAsync(older, newer);
		return (older, newer);
	}

	private async Task AgeAsync(MediaId older, MediaId newer)
	{
		await host.AddedAsync(older, Now);
		await host.AddedAsync(newer, Now.AddHours(1));
	}

	private static FeedAbsenceRow Absence(FeedId feed, MediaId title, int missed)
	{
		return new()
		{
			FeedId = feed.Value,
			MediaId = title.Value,
			MissedRuns = missed,
		};
	}

	private async Task DetailedAsync(MediaId id, Action<MediaDetailsRow> change)
	{
		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		var details = await ctx.MediaDetails.Include(row => row.Ratings).Include(row => row.PageLinks).SingleAsync(row => row.MediaId == id.Value);
		change(details);
		await ctx.SaveChangesAsync();
	}

	private async Task WantedAsync(MediaId id, DateTimeOffset at)
	{
		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await ctx
			.Wantlist.IgnoreQueryFilters()
			.Where(wanted => wanted.Media.Id == id.Value)
			.ExecuteUpdateAsync(setters => setters.SetProperty(wanted => wanted.CreatedAt, at));
	}

	private static MediaSpec Spec(string title, params ProviderId[] ids) => new(title, MediaType.Movie, ids);

	private static IIdentityKey Tmdb(string value) => TmdbId.From(value, MediaType.Movie);

	private static IIdentityKey Imdb(string value) => ImdbId.From(value, MediaType.Movie);
}
