using Contracts.Media;
using Domain.Media;
using Harness;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestInfrastructure;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaConfirmedMissingMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BeforeConfirmedGaps = "20260930221057_RenameWantlistSinkBlocklist";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Applying_ConfirmsTheTaglineAndBackdropGapsTheOldRuleAlreadyLeftAlone()
	{
		var movie = await host.Movie(TestMedia.Inception).SeedAsync();
		await SeedAsync(
			movie,
			new MovieDetails
			{
				Title = "Inception",
				Tagline = " \t",
				Summary = "Dreams within dreams.",
				PosterUrl = "poster",
				ReleaseDate = new DateOnly(2010, 7, 16),
				Genres = ["Science Fiction"],
				Runtime = TimeSpan.FromMinutes(148),
			}
		);

		await MigrateAsync(BeforeConfirmedGaps);
		await MigrateAsync(null);

		await Assert.That(await ConfirmedAbsentAsync(movie)).IsEqualTo(MediaField.Tagline | MediaField.Backdrop);
	}

	[Test]
	public async Task Applying_LeavesATitleWithAnyOtherGapUnconfirmed()
	{
		var show = await host.Show(TestMedia.BreakingBad).SeedAsync();
		await SeedAsync(
			show,
			new ShowDetails
			{
				Title = "Breaking Bad",
				Genres = ["Drama"],
				Seasons = 5,
			}
		);

		await MigrateAsync(BeforeConfirmedGaps);
		await MigrateAsync(null);

		await Assert.That(await ConfirmedAbsentAsync(show)).IsEqualTo(MediaField.None);
	}

	private Task SeedAsync(MediaId id, MediaDetails details)
	{
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
