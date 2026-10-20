using Contracts.Media;
using DB.Data.Media;
using Domain.Media;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ShowPartRepositoryTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly IExternalMediaId SeasonOnAniList = AniListId.From("185660", MediaType.Show);

	private static readonly IExternalMediaId SeasonOnMal = MalId.From("59130", MediaType.Show);

	private static readonly ShowPartPlace Unplaced = new(new None(), new None());

	private static readonly string[] ShowKeys = ["AniList:Show:161645", "Tvdb:Show:431162"];

	private static readonly DateTimeOffset Asked = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

	private IShowParts Testee => host.Resolve<IShowParts>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetShowsAsync_ForAPartAddedWithBothIds_FindsTheShowsStoredKeysByEither()
	{
		var show = await ShowAsync();
		await Testee.AddAsync([SeasonOnAniList, SeasonOnMal], Some.Of(show), Unplaced);

		var found = await Testee.GetShowsAsync([SeasonOnAniList, SeasonOnMal]);

		await Assert.That(found.Keys).IsEquivalentTo([SeasonOnAniList, SeasonOnMal]);
		await Assert.That(await KeysAsync(found[SeasonOnAniList])).IsEquivalentTo(ShowKeys);
		await Assert.That(await KeysAsync(found[SeasonOnMal])).IsEquivalentTo(ShowKeys);
	}

	[Test]
	public async Task GetShowsAsync_ForAPartAskedAsAMovie_FindsTheShowsKeys()
	{
		var show = await ShowAsync();
		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), Unplaced);
		var asMovie = AniListId.From("185660", MediaType.Movie);

		var found = await Testee.GetShowsAsync([asMovie]);

		await Assert.That(await KeysAsync(found[asMovie])).IsEquivalentTo(ShowKeys);
	}

	[Test]
	public async Task GetShowsAsync_ForAPartNeverAdded_FindsNothing()
	{
		await ShowAsync();

		var found = await Testee.GetShowsAsync([SeasonOnAniList]);

		await Assert.That(found).IsEmpty();
	}

	[Test]
	public async Task GetShowsAsync_ForTheSameIdOnAnotherSource_FindsNothing()
	{
		var show = await ShowAsync();
		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), Unplaced);

		var found = await Testee.GetShowsAsync([MalId.From("185660", MediaType.Show)]);

		await Assert.That(found).IsEmpty();
	}

	[Test]
	public async Task GetShowsAsync_ForAPartAskedTwice_AnswersItOnce()
	{
		var show = await ShowAsync();
		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), Unplaced);

		var found = await Testee.GetShowsAsync([SeasonOnAniList, AniListId.From("185660", MediaType.Show)]);

		await Assert.That(found).Count().IsEqualTo(1);
	}

	[Test]
	public async Task AddAsync_ForAPartLinkedToAnotherShow_MovesTheLink()
	{
		var first = await ShowAsync();
		var second = await host.SeedMediaAsync(AniListId.From("21", MediaType.Show));
		await Testee.AddAsync([SeasonOnAniList], Some.Of(first), Unplaced);

		await Testee.AddAsync([SeasonOnAniList], Some.Of(second), Unplaced);

		var found = await Testee.GetShowsAsync([SeasonOnAniList]);
		await Assert.That(await KeysAsync(found[SeasonOnAniList])).IsEquivalentTo(["AniList:Show:21"]);
	}

	[Test]
	public async Task AddAsync_ForAPartAddedAgain_KeepsTheNewPlace()
	{
		var show = await ShowAsync();
		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), new ShowPartPlace(Some.Of(3), Some.Of(24)));

		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), new ShowPartPlace(Some.Of(2), new None()));

		var part = (await PartsAsync()).Single();
		await Assert.That((part.TvdbSeason, part.EpisodeOffset)).IsEqualTo((2, null));
	}

	[Test]
	public async Task AddAsync_ForAShowThatIsGone_LinksNothing()
	{
		await Testee.AddAsync([SeasonOnAniList], Some.Of(MediaId.New()), Unplaced);

		await Assert.That(await PartsAsync()).IsEmpty();
	}

	[Test]
	public async Task AddAsync_ForAShow_LeavesItsExternalIdsAsTheyWere()
	{
		var show = await ShowAsync();

		await Testee.AddAsync([SeasonOnAniList, SeasonOnMal], Some.Of(show), Unplaced);

		var stored = (await host.Resolve<IMediaRepository>().GetAsync([show])).Single();
		await Assert.That(stored.Identity.Keys.Select(id => $"{id}")).IsEquivalentTo(ShowKeys);
	}

	[Test]
	public async Task AddAsync_WithAPlace_StoresItsTvdbSeasonAndEpisodeOffset()
	{
		var show = await ShowAsync();

		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), new ShowPartPlace(Some.Of(3), Some.Of(24)));

		var part = (await PartsAsync()).Single();
		await Assert.That((part.TvdbSeason, part.EpisodeOffset)).IsEqualTo((3, 24));
	}

	[Test]
	public async Task GetShowsAsync_ForANoShowAnswerFromTheLastThreeDays_FindsNone()
	{
		host.SetCurrentTime(Asked);
		await Testee.AddAsync([SeasonOnAniList, SeasonOnMal], new None(), Unplaced);
		host.SetCurrentTime(Asked.AddDays(3).AddMinutes(-1));

		var found = await Testee.GetShowsAsync([SeasonOnAniList, SeasonOnMal]);

		await Assert.That(found[SeasonOnAniList]).IsNone();
		await Assert.That(found[SeasonOnMal]).IsNone();
	}

	[Test]
	public async Task GetShowsAsync_ForANoShowAnswerOlderThanThreeDays_FindsNothing()
	{
		host.SetCurrentTime(Asked);
		await Testee.AddAsync([SeasonOnAniList], new None(), Unplaced);
		host.SetCurrentTime(Asked.AddDays(3).AddMinutes(1));

		var found = await Testee.GetShowsAsync([SeasonOnAniList]);

		await Assert.That(found).IsEmpty();
	}

	[Test]
	public async Task GetShowsAsync_ForAPartLinkedLongAgo_StillFindsItsShow()
	{
		host.SetCurrentTime(Asked);
		var show = await ShowAsync();
		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), Unplaced);
		host.SetCurrentTime(Asked.AddDays(400));

		var found = await Testee.GetShowsAsync([SeasonOnAniList]);

		await Assert.That(await KeysAsync(found[SeasonOnAniList])).IsEquivalentTo(ShowKeys);
	}

	[Test]
	public async Task AddAsync_ForANoShowAnswerAskedAgain_RefreshesWhenItWasAsked()
	{
		host.SetCurrentTime(Asked);
		await Testee.AddAsync([SeasonOnAniList], new None(), Unplaced);
		host.SetCurrentTime(Asked.AddDays(2));
		await Testee.AddAsync([SeasonOnAniList], new None(), Unplaced);
		host.SetCurrentTime(Asked.AddDays(4));

		var found = await Testee.GetShowsAsync([SeasonOnAniList]);

		await Assert.That(found[SeasonOnAniList]).IsNone();
	}

	[Test]
	public async Task AddAsync_ForAPartAnsweredWithoutAShow_LinksItOnceItsShowIsFound()
	{
		var show = await ShowAsync();
		await Testee.AddAsync([SeasonOnAniList], new None(), Unplaced);

		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), Unplaced);

		var found = await Testee.GetShowsAsync([SeasonOnAniList]);
		await Assert.That(await KeysAsync(found[SeasonOnAniList])).IsEquivalentTo(ShowKeys);
	}

	[Test]
	public async Task AddAsync_ForANoShowAnswerOnALinkedPart_KeepsTheLink()
	{
		var show = await ShowAsync();
		await Testee.AddAsync([SeasonOnAniList], Some.Of(show), Unplaced);

		await Testee.AddAsync([SeasonOnAniList], new None(), Unplaced);

		var found = await Testee.GetShowsAsync([SeasonOnAniList]);
		await Assert.That(await KeysAsync(found[SeasonOnAniList])).IsEquivalentTo(ShowKeys);
	}

	private static async Task<IEnumerable<string>> KeysAsync(Optional<IReadOnlyList<IExternalMediaId>> show)
	{
		var keys = await Assert.That(show).IsSome();
		return keys!.Select(id => $"{id}");
	}

	private Task<MediaId> ShowAsync() => host.SeedMediaAsync(AniListId.From("161645", MediaType.Show), TvdbId.From("431162", MediaType.Show));

	private async Task<IReadOnlyList<ShowPartRow>> PartsAsync()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.ShowParts.AsNoTracking().ToListAsync();
	}
}
