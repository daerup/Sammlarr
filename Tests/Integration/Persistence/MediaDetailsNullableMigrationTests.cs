using Contracts.Media;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestInfrastructure;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaDetailsNullableMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BeforeNullableDetails = "20261002083510_RenameConfirmedMissingToConfirmedAbsent";
	private static readonly RatingSummary Rated = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8.9f, 13000)]);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Applying_TurnsEveryPlaceholderIntoUnknown()
	{
		var movie = await host.Movie(TestMedia.Inception).SeedAsync();
		var show = await host.Show(TestMedia.BreakingBad).SeedAsync();
		await MigrateAsync(BeforeNullableDetails);
		await host.Context.Database.ExecuteSqlAsync(
			$"""
			INSERT INTO "MediaMetadata" ("MediaId", "CreatedAt", "UpdatedAt", "LastFetchedAt", "Title", "Tagline", "Summary", "PosterUrl", "BackdropUrl", "ReleaseDate", "Genres", "MediaType", "ConfirmedAbsent", "Runtime")
			VALUES ({movie.Value}, 0, 0, 0, ' ', {"\r\n"}, {"\t"}, '', '  ', '0001-01-01', '[]', 'Movie', 'None', '00:00:00')
			"""
		);
		await host.Context.Database.ExecuteSqlAsync(
			$"""
			INSERT INTO "MediaMetadata" ("MediaId", "CreatedAt", "UpdatedAt", "LastFetchedAt", "Title", "Tagline", "Summary", "PosterUrl", "BackdropUrl", "ReleaseDate", "Genres", "MediaType", "ConfirmedAbsent", "Seasons", "Episodes", "RuntimePerEpisode")
			VALUES ({show.Value}, 0, 0, 0, '', '', '', '', '', '0001-01-01', '[]', 'Show', 'None', 0, 0, '00:00:00')
			"""
		);

		await MigrateAsync(null);

		await Assert.That(await DetailsAsync(movie)).IsEqualTo(new MovieDetails());
		await Assert.That(await DetailsAsync(show)).IsEqualTo(new ShowDetails());
	}

	[Test]
	public async Task Applying_KeepsRealValuesAndRatings()
	{
		var show = await host.Show(TestMedia.BreakingBad).SeedAsync();
		await MigrateAsync(BeforeNullableDetails);
		await host.Context.Database.ExecuteSqlAsync(
			$"""
			INSERT INTO "MediaMetadata" ("MediaId", "CreatedAt", "UpdatedAt", "LastFetchedAt", "Title", "Tagline", "Summary", "PosterUrl", "BackdropUrl", "ReleaseDate", "Genres", "MediaType", "ConfirmedAbsent", "Seasons", "Episodes", "RuntimePerEpisode")
			VALUES ({show.Value}, 0, 0, 0, 'Breaking Bad', 'Say my name.', 'A teacher turns to crime.', '/poster.jpg', '/backdrop.jpg', '2008-01-20', '["Drama"]', 'Show', 'None', 5, 62, '00:47:00')
			"""
		);
		await host.Context.Database.ExecuteSqlAsync(
			$"""INSERT INTO "MediaRatings" ("MediaId", "Source", "NormalizedValue", "VoteCount") VALUES ({show.Value}, 'Tmdb', 8.9, 13000)"""
		);

		await MigrateAsync(null);

		await Assert
			.That(await DetailsAsync(show))
			.IsEqualTo(
				new ShowDetails
				{
					Title = "Breaking Bad",
					Tagline = "Say my name.",
					Summary = "A teacher turns to crime.",
					PosterUrl = "/poster.jpg",
					BackdropUrl = "/backdrop.jpg",
					ReleaseDate = new DateOnly(2008, 1, 20),
					Genres = ["Drama"],
					Seasons = 5,
					Episodes = 62,
					RuntimePerEpisode = TimeSpan.FromMinutes(47),
					Rating = Rated,
				}
			);
	}

	[Test]
	public async Task RollingBack_WritesThePlaceholdersBackPerTypeAndKeepsTheRatings()
	{
		var movie = await host.Movie(TestMedia.Inception).SeedAsync();
		var show = await host.Show(TestMedia.BreakingBad).SeedAsync();
		await host.Resolve<IMediaRepository>()
			.UpsertDetailsAsync(
				new Dictionary<MediaId, CachedDetails>
				{
					[movie] = new(new MovieDetails { Rating = Rated }, DateTimeOffset.UtcNow, MediaField.None),
					[show] = new(new ShowDetails(), DateTimeOffset.UtcNow, MediaField.None),
				}
			);

		await MigrateAsync(BeforeNullableDetails);

		await Assert
			.That(
				await RawAsync(
					"""SELECT "Title" || '|' || "Tagline" || '|' || "Summary" || '|' || "PosterUrl" || '|' || "BackdropUrl" || '|' || "ReleaseDate" || '|' || "Runtime" AS "Value" FROM "MediaMetadata" WHERE "MediaType" = 'Movie'"""
				)
			)
			.IsEqualTo("|||||0001-01-01|00:00:00");
		await Assert
			.That(
				await RawAsync("""SELECT "Seasons" || '|' || "Episodes" || '|' || "RuntimePerEpisode" AS "Value" FROM "MediaMetadata" WHERE "MediaType" = 'Show'""")
			)
			.IsEqualTo("0|0|00:00:00");
		await Assert.That(await RawAsync("""SELECT CAST(COUNT(*) AS TEXT) AS "Value" FROM "MediaRatings" """)).IsEqualTo("1");

		await MigrateAsync(null);

		await Assert.That(await DetailsAsync(movie)).IsEqualTo(new MovieDetails { Rating = Rated });
		await Assert.That(await DetailsAsync(show)).IsEqualTo(new ShowDetails());
	}

	private async Task<MediaDetails> DetailsAsync(MediaId id)
	{
		var stored = (await host.Resolve<IMediaRepository>().GetAsync([id])).Single();
		var cached = await Assert.That(stored.Cached).IsSome();
		return cached!.Details;
	}

	private Task<string> RawAsync(string sql) => host.Context.Database.SqlQueryRaw<string>(sql).SingleAsync();

	private Task MigrateAsync(string? target) => host.Context.GetService<IMigrator>().MigrateAsync(target);
}
