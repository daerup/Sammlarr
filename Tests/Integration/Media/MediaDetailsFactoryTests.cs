using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaDetailsFactoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	private static readonly DateTimeOffset RefreshedAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
	private static readonly ProviderAnswer TmdbLackedTheTagline = new(RemoteService.Tmdb, MediaField.Tagline, RefreshedAt);
	private static readonly ProviderAnswer PlexLackedTheAudience = new(
		RemoteService.Plex,
		MediaFields.Rated([RatingSource.RottenTomatoesAudience]),
		RefreshedAt.AddDays(-3)
	);

	private readonly MediaDetailsFactory _testee = new();

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	[Test]
	public async Task BuildFrom_WhenMovieDetails_CopiesAllFields()
	{
		var details = MovieDetails();

		var entity = (MovieDetailsRow)_testee.BuildFrom(Cached(details), Guid.NewGuid());

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
		var details = ShowDetails();

		var entity = (ShowDetailsRow)_testee.BuildFrom(Cached(details), Guid.NewGuid());

		using var _ = Assert.Multiple();
		await Assert.That(entity.Title).IsEqualTo(details.Title);
		await Assert.That(entity.Seasons).IsEqualTo(details.Seasons);
		await Assert.That(entity.Episodes).IsEqualTo(details.Episodes);
		await Assert.That(entity.RuntimePerEpisode).IsEqualTo(details.RuntimePerEpisode);
	}

	[Test]
	public async Task BuildFrom_ForCachedDetails_CopiesBothTimesAndTheAnswers()
	{
		var asked = RefreshedAt.AddHours(2);

		var entity = _testee.BuildFrom(new CachedDetails(new MovieDetails { Title = "Solaris" }, RefreshedAt, asked, [TmdbLackedTheTagline]), Guid.NewGuid());

		await Assert.That(entity.RefreshedAt).IsEqualTo(RefreshedAt);
		await Assert.That(entity.AskedAt).IsEqualTo(asked);
		await Assert
			.That(entity.Answers.Select(answer => (answer.Provider, answer.Unfilled, answer.AnsweredAt)))
			.IsEquivalentTo([(RemoteService.Tmdb, (MediaFields)MediaField.Tagline, RefreshedAt)]);
	}

	[Test]
	public async Task BuildFrom_WhenDetailsHaveRatings_BuildsRatings()
	{
		var details = MovieDetails() with { Ratings = MediaRatings.From([new TmdbRating(8.4f, Some.Of(1234))]) };

		var entity = (MovieDetailsRow)_testee.BuildFrom(Cached(details), Guid.NewGuid());

		await Assert.That(entity.Ratings).Count().IsEqualTo(1);
		await Assert.That(entity.Ratings[0].Source).IsEqualTo(RatingSource.Tmdb);
		await Assert.That(entity.Ratings[0].NormalizedValue).IsEqualTo(8.4f);
	}

	[Test]
	public async Task Update_WhenSameType_MutatesScalarsInPlace()
	{
		var plexId = PlexId.From("plex-300", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(Cached(MovieDetails() with { Title = "Original" }), mediaId);
		var refreshed = RefreshedAt.AddDays(1);

		_testee.Update(existing, new CachedDetails(MovieDetails() with { Title = "Updated", Runtime = TimeSpan.FromMinutes(99) }, refreshed, refreshed, []));

		await Assert.That(existing.Title).IsEqualTo("Updated");
		await Assert.That(((MovieDetailsRow)existing).Runtime).IsEqualTo(TimeSpan.FromMinutes(99));
		await Assert.That(existing.RefreshedAt).IsEqualTo(refreshed);
	}

	[Test]
	public async Task Update_ReplacesTheAnswersAndKeepsWhenEachWasGiven()
	{
		var plexId = PlexId.From("plex-305", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(Cached(MovieDetails()) with { Answers = [TmdbLackedTheTagline, PlexLackedTheAudience] }, mediaId);
		var tmdbFilledEverything = new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, RefreshedAt.AddDays(1));

		_testee.Update(existing, Cached(MovieDetails()) with { Answers = [tmdbFilledEverything] });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.ProviderAnswers.AsNoTracking().SingleAsync();
		await Assert.That(new ProviderAnswer(stored.Provider, stored.Unfilled, stored.AnsweredAt)).IsEqualTo(tmdbFilledEverything);
	}

	[Test]
	public async Task Update_WhenSameType_ReplacesRatings()
	{
		var plexId = PlexId.From("plex-310", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(Cached(MovieDetails() with { Ratings = MediaRatings.From([new TmdbRating(5.0f, Some.Of(10))]) }), mediaId);

		_testee.Update(existing, Cached(MovieDetails() with { Ratings = MediaRatings.From([new TmdbRating(9.0f, Some.Of(9999))]) }));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.MediaRatings.AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Source).IsEqualTo(RatingSource.Tmdb);
		await Assert.That(stored.NormalizedValue).IsEqualTo(9.0f);
		await Assert.That(stored.VoteCount).IsEqualTo(9999);
	}

	[Test]
	public async Task Update_WhenNewRatingsAreEmpty_RemovesExistingRatings()
	{
		var plexId = PlexId.From("plex-315", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(Cached(MovieDetails() with { Ratings = MediaRatings.From([new TmdbRating(5.0f, Some.Of(10))]) }), mediaId);

		_testee.Update(existing, Cached(new MovieDetails { Title = "Title" }));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await Assert.That(await fixture.Context.MediaRatings.AsNoTracking().AnyAsync()).IsFalse();
	}

	[Test]
	public async Task Update_WhenTypeMismatch_Throws()
	{
		var plexId = PlexId.From("plex-320", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(Cached(MovieDetails()), mediaId);

		await Assert.That(() => _testee.Update(existing, Cached(ShowDetails()))).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ToCached_WhenMovieEntity_ReadsBackWhatItWrote()
	{
		var plexId = PlexId.From("plex-400", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var written = new CachedDetails(
			MovieDetails() with
			{
				Title = "Movie",
				Runtime = TimeSpan.FromMinutes(110),
			},
			RefreshedAt,
			RefreshedAt.AddHours(1),
			[TmdbLackedTheTagline, PlexLackedTheAudience]
		);
		var entity = await PersistAsync(written, mediaId);

		await Assert.That(_testee.ToCached(entity)).IsEqualTo(written);
	}

	[Test]
	public async Task ToCached_WhenShowEntity_ReadsBackWhatItWrote()
	{
		var plexId = PlexId.From("plex-410", MediaType.Show);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Cached(ShowDetails()), mediaId);

		await Assert.That(_testee.ToCached(entity)).IsEqualTo(Cached(ShowDetails()));
	}

	[Test]
	public async Task ToCached_PassesThroughRatings()
	{
		var plexId = PlexId.From("plex-420", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Cached(MovieDetails() with { Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(500))]) }), mediaId);

		var cached = _testee.ToCached(entity);

		using var _ = Assert.Multiple();
		await Assert.That(cached.Details.Ratings.All.Count).IsEqualTo(1);
		await Assert.That(cached.Details.Ratings.All.Single().Source).IsEqualTo(RatingSource.Tmdb);
	}

	[Test]
	public async Task BuildFrom_WhenSomeFieldsAreMissing_WritesTheValuesItHas()
	{
		var plexId = PlexId.From("plex-420", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var details = new MovieDetails
		{
			Title = "Partial Title",
			Tagline = "Partial Tagline",
			Summary = "Partial Summary",
			PosterUrl = "https://example.com/partial-poster.jpg",
			ReleaseDate = new DateOnly(2019, 5, 4),
			Runtime = TimeSpan.FromMinutes(95),
		};

		var entity = await PersistAsync(Cached(details), mediaId);

		using var _ = Assert.Multiple();
		await Assert.That(entity.Title).IsEqualTo("Partial Title");
		await Assert.That(entity.Tagline).IsEqualTo("Partial Tagline");
		await Assert.That(entity.Summary).IsEqualTo("Partial Summary");
		await Assert.That(entity.PosterUrl).IsEqualTo("https://example.com/partial-poster.jpg");
		await Assert.That(entity.ReleaseDate).IsEqualTo(new DateOnly(2019, 5, 4));
		await Assert.That(((MovieDetailsRow)entity).Runtime).IsEqualTo(TimeSpan.FromMinutes(95));
	}

	[Test]
	public async Task ToCached_WhenAMovieRowHasOnlyATitle_ReadsTheRestAsUnknown()
	{
		var plexId = PlexId.From("plex-421", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Cached(new MovieDetails { Title = "Only a title" }), mediaId);

		await Assert.That(_testee.ToCached(entity).Details).IsEqualTo(new MovieDetails { Title = "Only a title" });
	}

	[Test]
	public async Task ToCached_WhenAShowRowHasOnlyATitle_ReadsTheRestAsUnknown()
	{
		var plexId = PlexId.From("plex-422", MediaType.Show);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Cached(new ShowDetails { Title = "Only a title" }), mediaId);

		await Assert.That(_testee.ToCached(entity).Details).IsEqualTo(new ShowDetails { Title = "Only a title" });
	}

	[Test]
	public async Task BuildFrom_WhenAShowHasCounts_WritesThem()
	{
		var plexId = PlexId.From("plex-423", MediaType.Show);
		var mediaId = await SeedMediaAsync([plexId]);
		var details = new ShowDetails
		{
			Title = "Partial Show",
			Seasons = 4,
			Episodes = 40,
			RuntimePerEpisode = TimeSpan.FromMinutes(22),
		};

		var entity = (ShowDetailsRow)await PersistAsync(Cached(details), mediaId);

		using var _ = Assert.Multiple();
		await Assert.That(entity.Seasons).IsEqualTo(4);
		await Assert.That(entity.Episodes).IsEqualTo(40);
		await Assert.That(entity.RuntimePerEpisode).IsEqualTo(TimeSpan.FromMinutes(22));
	}

	private async Task<MediaDetailsRow> PersistAsync(CachedDetails cached, MediaId mediaId)
	{
		var entity = _testee.BuildFrom(cached, mediaId.Value);
		fixture.Context.MediaDetails.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		return await fixture.Context.MediaDetails.Include(m => m.Ratings).Include(m => m.Answers).SingleAsync(m => m.MediaId == mediaId.Value);
	}

	private static CachedDetails Cached(MediaDetails details) => new(details, RefreshedAt, RefreshedAt, []);

	private static MovieDetails MovieDetails()
	{
		return new()
		{
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(100))]),
			Runtime = TimeSpan.FromMinutes(120),
		};
	}

	private static ShowDetails ShowDetails()
	{
		return new()
		{
			Title = "Show Title",
			Tagline = "Show Tagline",
			Summary = "Show Summary",
			PosterUrl = "https://example.com/show-poster.jpg",
			BackdropUrl = "https://example.com/show-backdrop.jpg",
			ReleaseDate = new DateOnly(2020, 1, 1),
			Genres = ["Drama"],
			Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(100))]),
			Seasons = 3,
			Episodes = 30,
			RuntimePerEpisode = TimeSpan.FromMinutes(45),
		};
	}
}
