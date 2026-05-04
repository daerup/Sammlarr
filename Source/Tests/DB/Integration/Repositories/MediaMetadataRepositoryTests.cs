using Contracts.Media;
using Contracts.Media.Search;
using DB.Data.MediaGroup;
using DB.Data.Metadata;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaMetadataRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private MediaMetadataRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup() => _testee = new MediaMetadataRepository(fixture.Context, new MediaIdGroupRepository(fixture.Context));

	[Test]
	public async Task UpsertAsync_WhenNoEntryExists_PersistsMovieMetadata()
	{
		var anchor = TmdbId.From("100", MediaType.Movie);
		var imdbId = ImdbId.From("tt100", MediaType.Movie);
		var details = CreateMovieDetails(anchor, [imdbId]);

		await _testee.UpsertAsync(anchor, details);

		var stored = await fixture.Context.MediaMetadata.OfType<MovieMetadataEntity>().AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Title).IsEqualTo(details.Title);
		await Assert.That(stored.Tagline).IsEqualTo(details.Tagline);
		await Assert.That(stored.Summary).IsEqualTo(details.Summary);
		await Assert.That(stored.PosterUrl).IsEqualTo(details.PosterUrl);
		await Assert.That(stored.BackdropUrl).IsEqualTo(details.BackdropUrl);
		await Assert.That(stored.Runtime).IsEqualTo(details.Runtime);
		await Assert.That(stored.ReleaseDate).IsEqualTo(details.ReleaseDate);
		await Assert.That(stored.Genres).IsEquivalentTo(details.Genres);
	}

	[Test]
	public async Task UpsertAsync_WhenEntryExists_ReplacesEntry()
	{
		var anchor = TmdbId.From("110", MediaType.Movie);
		var first = CreateMovieDetails(anchor, []) with { Title = "First" };
		var second = CreateMovieDetails(anchor, []) with { Title = "Second" };

		await _testee.UpsertAsync(anchor, first);
		fixture.Context.ChangeTracker.Clear();
		await _testee.UpsertAsync(anchor, second);

		var stored = await fixture.Context.MediaMetadata.AsNoTracking().SingleAsync();
		await Assert.That(stored.Title).IsEqualTo("Second");
	}

	[Test]
	public async Task GetByMediaIdAsync_WhenAnchorMatches_ReturnsCachedDetails()
	{
		var anchor = TmdbId.From("120", MediaType.Movie);
		var imdbId = ImdbId.From("tt120", MediaType.Movie);
		var details = CreateMovieDetails(anchor, [imdbId]);
		await _testee.UpsertAsync(anchor, details);
		fixture.Context.ChangeTracker.Clear();

		var cached = await _testee.GetByMediaIdAsync(anchor);

		await Assert.That(cached).IsNotNull();
		await Assert.That(cached!.Details).IsTypeOf<MovieDetailResult>();
		var result = (MovieDetailResult)cached.Details;
		using var _ = Assert.Multiple();
		await Assert.That(result.SourceId).IsEqualTo(anchor);
		await Assert.That(result.AdditionalIds).Contains(imdbId);
		await Assert.That(result.Title).IsEqualTo(details.Title);
		await Assert.That(result.Runtime).IsEqualTo(details.Runtime);
	}

	[Test]
	public async Task GetByMediaIdAsync_WhenLookedUpByLinkedId_ReturnsDetailsWithThatIdAsAnchor()
	{
		var anchor = TmdbId.From("130", MediaType.Movie);
		var imdbId = ImdbId.From("tt130", MediaType.Movie);
		var details = CreateMovieDetails(anchor, [imdbId]);
		await _testee.UpsertAsync(anchor, details);
		fixture.Context.ChangeTracker.Clear();

		var cached = await _testee.GetByMediaIdAsync(imdbId);

		await Assert.That(cached).IsNotNull();
		var result = (MovieDetailResult)cached!.Details;
		await Assert.That(result.SourceId).IsEqualTo((IMediaId)imdbId);
		await Assert.That(result.AdditionalIds).Contains(anchor);
	}

	[Test]
	public async Task GetByMediaIdAsync_WhenNoEntryExists_ReturnsNull()
	{
		var cached = await _testee.GetByMediaIdAsync(TmdbId.From("999", MediaType.Movie));

		await Assert.That(cached).IsNull();
	}

	[Test]
	public async Task UpsertAsync_WhenShowDetails_PersistsAsShowEntity()
	{
		var anchor = TmdbId.From("200", MediaType.Show);
		var details = CreateShowDetails(anchor);

		await _testee.UpsertAsync(anchor, details);
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.MediaMetadata.OfType<ShowMetadataEntity>().AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Seasons).IsEqualTo(details.Seasons);
		await Assert.That(stored.Episodes).IsEqualTo(details.Episodes);
		await Assert.That(stored.RuntimePerEpisode).IsEqualTo(details.RuntimePerEpisode);
	}

	[Test]
	public async Task GetByMediaIdAsync_WhenShowEntryExists_ReturnsShowDetailResult()
	{
		var anchor = TmdbId.From("210", MediaType.Show);
		var details = CreateShowDetails(anchor);
		await _testee.UpsertAsync(anchor, details);
		fixture.Context.ChangeTracker.Clear();

		var cached = await _testee.GetByMediaIdAsync(anchor);

		await Assert.That(cached).IsNotNull();
		await Assert.That(cached!.Details).IsTypeOf<ShowDetailResult>();
		var show = (ShowDetailResult)cached.Details;
		await Assert.That(show.Seasons).IsEqualTo(details.Seasons);
		await Assert.That(show.Episodes).IsEqualTo(details.Episodes);
	}

	[Test]
	public async Task RemoveAsync_WhenEntryExists_DeletesEntryButKeepsMediaIdGroup()
	{
		var anchor = TmdbId.From("300", MediaType.Movie);
		var details = CreateMovieDetails(anchor, [ImdbId.From("tt300", MediaType.Movie)]);
		await _testee.UpsertAsync(anchor, details);
		fixture.Context.ChangeTracker.Clear();

		await _testee.RemoveAsync(anchor);

		await Assert.That(await fixture.Context.MediaMetadata.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.MediaIdGroups.AnyAsync()).IsTrue();
	}

	[Test]
	public async Task RemoveAsync_WhenNoEntryExists_DoesNotThrow()
	{
		await Assert.That(async () => await _testee.RemoveAsync(TmdbId.From("0", MediaType.Movie))).ThrowsNothing();
	}

	private static MovieDetailResult CreateMovieDetails(IMediaId sourceId, IMediaId[] additionalIds) =>
		new()
		{
			SourceId = sourceId,
			AdditionalIds = additionalIds,
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

	private static ShowDetailResult CreateShowDetails(IMediaId sourceId) =>
		new()
		{
			SourceId = sourceId,
			AdditionalIds = [],
			Title = "Show Title",
			Tagline = "Show Tagline",
			Summary = "Show Summary",
			PosterUrl = "https://example.com/show-poster.jpg",
			BackdropUrl = "https://example.com/show-backdrop.jpg",
			ReleaseDate = new DateOnly(2020, 1, 1),
			Genres = ["Drama"],
			Seasons = 3,
			Episodes = 30,
			RuntimePerEpisode = TimeSpan.FromMinutes(45),
		};
}
