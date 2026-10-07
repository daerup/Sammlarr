using System.Text;
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
	public async Task FindAsync_AfterAReplace_FindsAnEntryByItsAniListOrItsMalId()
	{
		var map = host.Resolve<IAnimeIds>();
		var entry = new AnimeIdMapping(MediaType.Show, Some.Of(290), Some.Of(290), Some.Of(72025), Some.Of(26209), Some.Of("tt0286390"));

		await map.ReplaceAsync(new[] { entry }.ToAsyncEnumerable());

		await Assert.That(await map.FindAsync(AniListId.From("290", MediaType.Show))).IsSome().And.IsEqualTo(entry);
		await Assert.That(await map.FindAsync(MalId.From("290", MediaType.Show))).IsSome().And.IsEqualTo(entry);
		await Assert.That(await map.FindAsync(AniListId.From("1", MediaType.Show))).IsNone();
	}

	[Test]
	public async Task FindIdAsync_ForAShowWithOnlyAnAniListId_FindsItsTvdbIdOnceTheMapLoaded()
	{
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.AnimeLists}/anime-list-full.json", Map);
		var show = await host.SeedMediaAsync(AniListId.From("290", MediaType.Show));
		var media = host.Resolve<IMediaService>();

		var before = await media.FindIdAsync(show, MediaProvider.Tvdb);
		await host.Resolve<IEnumerable<IDatasetLoader>>().Single(loader => loader.Name == DatasetNames.AnimeIds).LoadAsync();
		host.SetCurrentTime(new DateTimeOffset(2024, 6, 1, 2, 0, 0, TimeSpan.Zero));
		var after = await media.FindIdAsync(show, MediaProvider.Tvdb);

		await Assert.That(before).IsNone();
		await Assert.That(after).IsSome().And.IsEqualTo<IExternalMediaId>(TvdbId.From("72025", MediaType.Show));
	}

	[Test]
	public async Task FindIdAsync_ForALaterSeason_FindsNoTvdbIdAndLeavesTheShowAlone()
	{
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.AnimeLists}/anime-list-full.json", Map);
		await host.Resolve<IEnumerable<IDatasetLoader>>().Single(loader => loader.Name == DatasetNames.AnimeIds).LoadAsync();
		var first = await host.SeedMediaAsync(AniListId.From("290", MediaType.Show));
		var second = await host.SeedMediaAsync(AniListId.From("291", MediaType.Show));
		var media = host.Resolve<IMediaService>();

		var shows = (await media.FindIdAsync(first, MediaProvider.Tvdb), await media.FindIdAsync(second, MediaProvider.Tvdb));

		await Assert.That(shows.Item1).IsSome();
		await Assert.That(shows.Item2).IsNone();
	}
}
