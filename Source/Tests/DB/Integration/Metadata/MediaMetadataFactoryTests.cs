using Contracts;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using DB.Data.MediaGroup;
using DB.Data.Metadata;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Metadata;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaMetadataFactoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private MediaMetadataFactory _testee = null!;
	private MediaIdGroupRepository _groupRepo = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
		_testee = new MediaMetadataFactory(fixture.Context, dateTimeProvider.Object);
		_groupRepo = new MediaIdGroupRepository(fixture.Context);
	}

	[Test]
	public async Task BuildFrom_WhenMovieDetails_CopiesAllFields()
	{
		var details = CreateMovieDetails(TmdbId.From("100", MediaType.Movie));

		var entity = _testee.BuildFrom<MovieMetadataEntity>(details, Guid.NewGuid());

		using var _ = Assert.Multiple();
		await Assert.That(entity.Title).IsEqualTo(details.Title);
		await Assert.That(entity.Tagline).IsEqualTo(details.Tagline);
		await Assert.That(entity.Summary).IsEqualTo(details.Summary);
		await Assert.That(entity.PosterUrl).IsEqualTo(details.PosterUrl);
		await Assert.That(entity.BackdropUrl).IsEqualTo(details.BackdropUrl);
		await Assert.That(entity.ReleaseDate).IsEqualTo(details.ReleaseDate);
		await Assert.That(entity.Genres).IsEquivalentTo(details.Genres);
		await Assert.That(entity.Runtime).IsEqualTo(details.Runtime);
	}

	[Test]
	public async Task BuildFrom_WhenShowDetails_CopiesAllFields()
	{
		var details = CreateShowDetails(TmdbId.From("200", MediaType.Show));

		var entity = _testee.BuildFrom<ShowMetadataEntity>(details, Guid.NewGuid());

		using var _ = Assert.Multiple();
		await Assert.That(entity.Title).IsEqualTo(details.Title);
		await Assert.That(entity.Seasons).IsEqualTo(details.Seasons);
		await Assert.That(entity.Episodes).IsEqualTo(details.Episodes);
		await Assert.That(entity.RuntimePerEpisode).IsEqualTo(details.RuntimePerEpisode);
	}

	[Test]
	public async Task BuildFrom_WhenDetailsHaveRatings_BuildsRatings()
	{
		var ratings = new[] { new MediaRating(RatingSource.Tmdb, 8.4f, 1234) };
		var details = CreateMovieDetails(TmdbId.From("110", MediaType.Movie)) with { Ratings = ratings };

		var entity = _testee.BuildFrom<MovieMetadataEntity>(details, Guid.NewGuid());

		await Assert.That(entity.Ratings).Count().IsEqualTo(1);
		await Assert.That(entity.Ratings[0].Source).IsEqualTo(RatingSource.Tmdb);
		await Assert.That(entity.Ratings[0].NormalizedValue).IsEqualTo(8.4f);
	}

	[Test]
	public async Task Update_WhenSameType_MutatesScalarsInPlace()
	{
		var mediaId = TmdbId.From("300", MediaType.Movie);
		var existing = await PersistAsync(CreateMovieDetails(mediaId) with { Title = "Original" });

		_testee.Update(existing, CreateMovieDetails(mediaId) with { Title = "Updated", Runtime = TimeSpan.FromMinutes(99) });

		await Assert.That(existing.Title).IsEqualTo("Updated");
		await Assert.That(((MovieMetadataEntity)existing).Runtime).IsEqualTo(TimeSpan.FromMinutes(99));
	}

	[Test]
	public async Task Update_WhenSameType_ReplacesRatings()
	{
		var mediaId = TmdbId.From("310", MediaType.Movie);
		var existing = await PersistAsync(CreateMovieDetails(mediaId) with { Ratings = [new MediaRating(RatingSource.Tmdb, 5.0f, 10)] });

		_testee.Update(existing, CreateMovieDetails(mediaId) with { Ratings = [new MediaRating(RatingSource.Imdb, 9.0f, 9999)] });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.MediaRatings.AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Source).IsEqualTo(RatingSource.Imdb);
		await Assert.That(stored.NormalizedValue).IsEqualTo(9.0f);
	}

	[Test]
	public async Task Update_WhenTypeMismatch_Throws()
	{
		var mediaId = TmdbId.From("320", MediaType.Movie);
		var existing = await PersistAsync(CreateMovieDetails(mediaId));
		var showDetails = CreateShowDetails(TmdbId.From("321", MediaType.Show));

		await Assert.That(() => _testee.Update(existing, showDetails)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ToDetailResult_WhenMovieEntity_ReturnsMovieDetailWithMappedFields()
	{
		var mediaId = TmdbId.From("400", MediaType.Movie);
		var entity = await PersistAsync(CreateMovieDetails(mediaId) with { Title = "Movie", Runtime = TimeSpan.FromMinutes(110) });

		var result = (MovieDetailResult)_testee.ToDetailResult(entity, mediaId, []);

		using var _ = Assert.Multiple();
		await Assert.That(result.SourceId).IsEqualTo((IMediaId)mediaId);
		await Assert.That(result.Title).IsEqualTo("Movie");
		await Assert.That(result.Runtime).IsEqualTo(TimeSpan.FromMinutes(110));
	}

	[Test]
	public async Task ToDetailResult_WhenShowEntity_ReturnsShowDetailWithMappedFields()
	{
		var mediaId = TmdbId.From("410", MediaType.Show);
		var entity = await PersistAsync(CreateShowDetails(mediaId));

		var result = (ShowDetailResult)_testee.ToDetailResult(entity, mediaId, []);

		using var _ = Assert.Multiple();
		await Assert.That(result.Seasons).IsEqualTo(3);
		await Assert.That(result.Episodes).IsEqualTo(30);
		await Assert.That(result.RuntimePerEpisode).IsEqualTo(TimeSpan.FromMinutes(45));
	}

	[Test]
	public async Task ToDetailResult_PassesThroughAdditionalIdsAndRatings()
	{
		var mediaId = TmdbId.From("420", MediaType.Movie);
		var imdbId = ImdbId.From("tt420", MediaType.Movie);
		var entity = await PersistAsync(CreateMovieDetails(mediaId) with { Ratings = [new MediaRating(RatingSource.Tmdb, 7.5f, 500)] });

		var result = _testee.ToDetailResult(entity, mediaId, [imdbId]);

		using var _ = Assert.Multiple();
		await Assert.That(result.AdditionalIds).Contains(imdbId);
		await Assert.That(result.Ratings).Count().IsEqualTo(1);
		await Assert.That(result.Ratings[0].Source).IsEqualTo(RatingSource.Tmdb);
	}

	private async Task<MediaMetadataEntity> PersistAsync(IMediaDetailResult details)
	{
		await _groupRepo.UpsertAsync(details.AdditionalIds.Prepend(details.SourceId));
		var identifier = MediaIdentifier.From(details.SourceId);
		var group = await fixture
			.Context.MediaIdEntries.Include(e => e.MediaIdGroupEntity)
			.Where(e => e.Identifier == identifier)
			.Select(e => e.MediaIdGroupEntity)
			.SingleAsync();

		var entity = _testee.BuildFrom<MediaMetadataEntity>(details, group.Id);
		fixture.Context.MediaMetadata.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		return await fixture.Context.MediaMetadata.Include(m => m.Ratings).SingleAsync(m => m.MediaIdGroupId == group.Id);
	}

	private static MovieDetailResult CreateMovieDetails(IMediaId sourceId) =>
		new()
		{
			SourceId = sourceId,
			AdditionalIds = [],
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
