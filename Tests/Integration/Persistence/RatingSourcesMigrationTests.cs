using Contracts.Media;
using Domain.Media;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestInfrastructure;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class RatingSourcesMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BeforeRatingSources = "20261003104425_MediaDetailsPlaceholdersToNull";

	private const MediaField NewRatingGaps = MediaField.ImdbRating | MediaField.RottenTomatoesCriticsRating | MediaField.RottenTomatoesAudienceRating;

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	[Arguments("Tagline, Rating", MediaField.Tagline | MediaField.TmdbRating)]
	[Arguments("Rating", MediaField.TmdbRating)]
	[Arguments("None", MediaField.None)]
	public async Task Applying_RenamesTheOldRatingGapAndConfirmsTheSourcesItAdds(string stored, MediaField kept)
	{
		var movie = await host.Movie(TestMedia.Inception).SeedAsync();
		await SeedAsync(movie);
		await MigrateAsync(BeforeRatingSources);
		await host.Context.Database.ExecuteSqlAsync($"""UPDATE "MediaDetails" SET "ConfirmedAbsent" = {stored}""");

		await MigrateAsync(null);

		await Assert.That(await ConfirmedAbsentAsync(movie)).IsEqualTo(kept | NewRatingGaps);
	}

	[Test]
	public async Task Reverting_KeepsOnlyTheTmdbRatingsTheOldModelReads()
	{
		var movie = await host.Movie(TestMedia.Inception).SeedAsync();
		await SeedAsync(
			movie,
			RatingSummary.From([new TmdbRating(8.8f, Some.Of(38000)), new ImdbRating(8.8f, Some.Of(2_700_000)), new RottenTomatoesCriticsRating(87)])
		);

		await MigrateAsync(BeforeRatingSources);

		var sources = await host.Context.Database.SqlQuery<string>($"""SELECT "Source" AS "Value" FROM "MediaRatings" """).ToListAsync();
		await Assert.That(sources).IsEquivalentTo(["Tmdb"]);
	}

	private Task SeedAsync(MediaId id, RatingSummary? rating = null)
	{
		var details = new MovieDetails { Title = "Inception", Rating = rating ?? RatingSummary.None };
		return host.Resolve<IMediaRepository>()
			.UpsertDetailsAsync(new Dictionary<MediaId, CachedDetails> { [id] = new(details, DateTimeOffset.UtcNow, MediaField.None) });
	}

	private async Task<MediaField> ConfirmedAbsentAsync(MediaId id)
	{
		var stored = (await host.Resolve<IMediaRepository>().GetAsync([id])).Single();
		var cached = await Assert.That(stored.Cached).IsSome();
		return cached!.ConfirmedAbsent;
	}

	private Task MigrateAsync(string? target) => host.Context.GetService<IMigrator>().MigrateAsync(target);
}
