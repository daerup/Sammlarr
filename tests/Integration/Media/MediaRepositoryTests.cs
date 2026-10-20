using Contracts.Media;
using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using TUnit.Assertions.Enums;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IMediaRepository _testee = null!;

	[Before(Test)]
	public void Setup()
	{
		_testee = new MediaRepository(fixture.ContextFactory, new MediaDetailsFactory(), new MediaWriteLock(), fixture.Resolve<TimeProvider>());
	}

	[Test]
	public async Task GetExternalsAsync_WhenMediaExists_ReturnsExternalForRequestedSource()
	{
		var ids = new IIdentityKey[] { TmdbId.From("210", MediaType.Movie), PlexId.From("plex-210", MediaType.Movie) };
		var mediaId = await fixture.SeedMediaAsync(ids);

		var result = await _testee.GetExternalsAsync([mediaId], MediaProvider.Plex);

		await Assert.That(result.Count).IsEqualTo(1);
		await Assert.That(result[mediaId]).IsEqualTo(PlexId.From("plex-210", MediaType.Movie));
	}

	[Test]
	public async Task GetGenresAsync_ForStoredTitles_CountsEachGenreMostUsedFirst()
	{
		await StoreAsync(TmdbId.From("550", MediaType.Movie), "Drama", "Thriller");
		await StoreAsync(TmdbId.From("551", MediaType.Movie), "Drama");
		await StoreAsync(TmdbId.From("552", MediaType.Movie), "Anime", "Drama");
		await StoreAsync(TmdbId.From("553", MediaType.Movie));

		var genres = await _testee.GetGenresAsync();

		await Assert
			.That(genres)
			.IsEquivalentTo([new GenreCount("Drama", 3), new GenreCount("Anime", 1), new GenreCount("Thriller", 1)], CollectionOrdering.Matching);
	}

	private async Task StoreAsync(TmdbId id, params string[] genres)
	{
		var mediaId = await fixture.SeedMediaAsync([id]);
		var details = new MovieDetails { Title = id.Value, Genres = [.. genres] };
		var now = fixture.Resolve<TimeProvider>().GetUtcNow();
		await _testee.SaveDetailsAsync([new DetailsChange(mediaId, new None(), new CachedDetails(details, now, now, []))]);
	}
}
