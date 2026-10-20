using Contracts.Datasets;
using Contracts.Media;
using Core.Datasets;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Datasets;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class AnimeIdsTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Map = """
		[{"type":"TV","anidb_id":1,"anilist_id":290,"mal_id":290,"imdb_id":["tt0286390"],"themoviedb_id":{"tv":26209},"tvdb_id":72025,"season":{"tvdb":1,"tmdb":1}},
		{"type":"TV","anidb_id":2,"anilist_id":291,"mal_id":291,"tvdb_id":72025,"season":{"tvdb":2,"tmdb":2}}]
		""";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task FindAsync_AfterAReplace_FindsAnEntryByItsAniListMalOrTvdbId()
	{
		var map = host.Resolve<IAnimeIds>();
		var entry = new AnimeIdMapping(MediaType.Show, Some.Of(290), Some.Of(290), Some.Of(72025), Some.Of(26209), Some.Of("tt0286390"), new None());

		await map.ReplaceAsync(new[] { entry }.ToAsyncEnumerable());

		await Assert.That(await map.FindAsync(AniListId.From("290", MediaType.Show))).IsSome().And.IsEqualTo(entry);
		await Assert.That(await map.FindAsync(MalId.From("290", MediaType.Show))).IsSome().And.IsEqualTo(entry);
		await Assert.That(await map.FindAsync(TvdbId.From("72025", MediaType.Show))).IsSome().And.IsEqualTo(entry);
		await Assert.That(await map.FindAsync(AniListId.From("1", MediaType.Show))).IsNone();
	}

	[Test]
	public async Task FindAsync_ByAShowsOrMoviesTmdbOrImdbId_FindsTheEntryOfThatTypeOnly()
	{
		var map = host.Resolve<IAnimeIds>();
		var show = new AnimeIdMapping(MediaType.Show, Some.Of(16498), Some.Of(16498), Some.Of(267440), Some.Of(1429), Some.Of("tt2560140"), new None());
		var movie = new AnimeIdMapping(MediaType.Movie, Some.Of(199), Some.Of(199), new None(), Some.Of(1429), Some.Of("tt0245429"), new None());

		await map.ReplaceAsync(new[] { show, movie }.ToAsyncEnumerable());

		await Assert.That(await map.FindAsync(TvdbId.From("267440", MediaType.Show))).IsSome().And.IsEqualTo(show);
		await Assert.That(await map.FindAsync(TmdbId.From("1429", MediaType.Movie))).IsSome().And.IsEqualTo(movie);
		await Assert.That(await map.FindAsync(TmdbId.From("1429", MediaType.Show))).IsSome().And.IsEqualTo(show);
		await Assert.That(await map.FindAsync(ImdbId.From("tt2560140", MediaType.Show))).IsSome().And.IsEqualTo(show);
		await Assert.That(await map.FindAsync(ImdbId.From("tt2560140", MediaType.Movie))).IsNone();
		await Assert.That(await map.FindAsync(TmdbId.From("550", MediaType.Movie))).IsNone();
	}

	[Test]
	public async Task FindManyAsync_ForATvdbIdWithSeveralSeasons_PicksTheSameEntryAsASingleRead()
	{
		var map = host.Resolve<IAnimeIds>();
		var first = new AnimeIdMapping(MediaType.Show, Some.Of(290), Some.Of(290), Some.Of(72025), Some.Of(26209), Some.Of("tt0286390"), new None());
		var second = new AnimeIdMapping(MediaType.Show, Some.Of(291), Some.Of(291), Some.Of(72025), new None(), new None(), new None());
		await map.ReplaceAsync(new[] { first, second }.ToAsyncEnumerable());
		var series = TvdbId.From("72025", MediaType.Show);
		var later = AniListId.From("291", MediaType.Show);

		var found = await map.FindManyAsync([series, later, AniListId.From("1", MediaType.Show)]);

		await Assert.That(found.Keys).IsEquivalentTo([series, (IExternalMediaId)later]);
		await Assert.That(found[series]).IsEqualTo(first);
		await Assert.That(await map.FindAsync(series)).IsSome().And.IsEqualTo(first);
		await Assert.That(found[later]).IsEqualTo(second);
	}

	[Test]
	public async Task FindManyAsync_ForEachKindOfId_PicksWhatASingleReadPicks()
	{
		var map = host.Resolve<IAnimeIds>();
		var show = new AnimeIdMapping(MediaType.Show, Some.Of(16498), Some.Of(16498), Some.Of(267440), Some.Of(1429), Some.Of("tt2560140"), new None());
		var movie = new AnimeIdMapping(MediaType.Movie, Some.Of(199), Some.Of(199), Some.Of(267440), Some.Of(1429), Some.Of("tt0245429"), new None());
		await map.ReplaceAsync(new[] { show, movie }.ToAsyncEnumerable());
		IExternalMediaId[] ids =
		[
			TvdbId.From("267440", MediaType.Show),
			TvdbId.From("267440", MediaType.Movie),
			TvdbId.From("267440", MediaType.Unknown),
			TmdbId.From("1429", MediaType.Movie),
			ImdbId.From("tt2560140", MediaType.Show),
			ImdbId.From("tt2560140", MediaType.Movie),
			AniListId.From("199", MediaType.Show),
			MalId.From("16498", MediaType.Movie),
			AniListId.From("not-a-number", MediaType.Show),
		];

		var found = await map.FindManyAsync(ids);

		foreach (var id in ids)
		{
			await Assert.That(found.GetValueOrNone(id)).IsEqualTo(await map.FindAsync(id));
		}
	}

	[Test]
	public async Task FindManyAsync_ForMoreIdsThanOneReadTakes_FindsEach()
	{
		var map = host.Resolve<IAnimeIds>();
		var entries = Enumerable
			.Range(1, 1500)
			.Select(id => new AnimeIdMapping(MediaType.Show, Some.Of(id), new None(), new None(), new None(), new None(), new None()))
			.ToList();
		await map.ReplaceAsync(entries.ToAsyncEnumerable());

		var found = await map.FindManyAsync([.. Enumerable.Range(1, 1500).Select(id => (IExternalMediaId)AniListId.From($"{id}", MediaType.Show))]);

		await Assert.That(found).Count().IsEqualTo(1500);
	}

	[Test]
	public async Task FindAsync_ForALaterSeason_KeepsTheSeriesItBelongsToAndItsPlace()
	{
		var map = host.Resolve<IAnimeIds>();
		var season = new AnimeIdMapping(MediaType.Show, Some.Of(291), Some.Of(291), new None(), new None(), new None(), Some.Of(72025))
		{
			TvdbSeason = Some.Of(3),
			EpisodeOffset = Some.Of(24),
		};

		await map.ReplaceAsync(new[] { season }.ToAsyncEnumerable());

		await Assert.That(await map.FindAsync(AniListId.From("291", MediaType.Show))).IsSome().And.IsEqualTo(season);
		await Assert.That(await map.FindAsync(TvdbId.From("72025", MediaType.Show))).IsNone();
	}

	[Test]
	public async Task FindIdAsync_ForAShowWithOnlyAnAniListId_FindsItsTvdbIdOnceTheMapLoaded()
	{
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.AnimeLists}/anime-list-full.json", Map);
		var show = await host.SeedMediaAsync(AniListId.From("290", MediaType.Show));
		var media = host.Resolve<IMediaService>();

		var before = await media.FindIdAsync(show, MediaProvider.Tvdb);
		await host.Resolve<IEnumerable<IDatasetLoader>>().Single(loader => loader.Name == DatasetNames.AnimeIds).LoadAsync(DatasetRead.WhenChanged);
		host.SetCurrentTime(new DateTimeOffset(2024, 6, 1, 2, 0, 0, TimeSpan.Zero));
		var after = await media.FindIdAsync(show, MediaProvider.Tvdb);

		await Assert.That(before).IsNone();
		await Assert.That(after).IsSome().And.IsEqualTo(TvdbId.From("72025", MediaType.Show));
	}

	[Test]
	public async Task FindIdAsync_ForALaterSeason_FindsNoTvdbIdAndLeavesTheShowAlone()
	{
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.AnimeLists}/anime-list-full.json", Map);
		await host.Resolve<IEnumerable<IDatasetLoader>>().Single(loader => loader.Name == DatasetNames.AnimeIds).LoadAsync(DatasetRead.WhenChanged);
		var first = await host.SeedMediaAsync(AniListId.From("290", MediaType.Show));
		var second = await host.SeedMediaAsync(AniListId.From("291", MediaType.Show));
		var media = host.Resolve<IMediaService>();

		var shows = (await media.FindIdAsync(first, MediaProvider.Tvdb), await media.FindIdAsync(second, MediaProvider.Tvdb));

		await Assert.That(shows.Item1).IsSome();
		await Assert.That(shows.Item2).IsNone();
	}
}
