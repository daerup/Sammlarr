using Contracts;
using Contracts.Media;
using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ExternalMediaIdRowTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Test]
	public async Task UniqueIndex_RejectsDuplicateIdentifier()
	{
		var firstMedia = new MediaRow();
		var secondMedia = new MediaRow();
		fixture.Context.ExternalMediaIds.Add(
			new ExternalMediaIdRow
			{
				Source = MediaProvider.Imdb,
				MediaType = MediaType.Show,
				Id = "tt9999",
				Media = firstMedia,
			}
		);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		fixture.Context.ExternalMediaIds.Add(
			new ExternalMediaIdRow
			{
				Source = MediaProvider.Imdb,
				MediaType = MediaType.Show,
				Id = "tt9999",
				Media = secondMedia,
			}
		);

		await Assert.That(async () => await fixture.Context.SaveChangesAsync()).Throws<DbUpdateException>();
	}

	[Test]
	public async Task OwnersAsync_ForKeysOfManyTitles_FindsEachOwnerOnceInOneQuery()
	{
		const int count = 2000;
		var titles = Enumerable.Range(0, count).Select(_ => new MediaRow()).ToList();
		var keys = titles.Select((media, index) => (Media: media, Key: TvdbId.From($"{index}", MediaType.Movie))).ToList();
		fixture.Context.ExternalMediaIds.AddRange(keys.Select(pair => pair.Key.ToExternalIdEntity(pair.Media)));
		fixture.Context.ExternalMediaIds.AddRange(keys.Select(pair => TmdbId.From(pair.Key.Value, MediaType.Movie).ToExternalIdEntity(pair.Media)));
		fixture.Context.ExternalMediaIds.Add(TmdbId.From("0", MediaType.Show).ToExternalIdEntity(new MediaRow()));
		fixture.Context.ExternalMediaIds.Add(ImdbId.From("0", MediaType.Movie).ToExternalIdEntity(new MediaRow()));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		IReadOnlyList<IExternalMediaId> asked =
		[
			.. keys.SelectMany(pair => new IExternalMediaId[] { pair.Key, TmdbId.From(pair.Key.Value, MediaType.Movie) }),
			ImdbId.From("tt0000001", MediaType.Movie),
		];

		var owners = await fixture.Context.OwnersAsync(asked, CancellationToken.None);

		await Assert.That(owners).IsEquivalentTo(titles.Select(media => media.Id));
	}

	[Test]
	public async Task SnapshotAsync_WhileADetailsCommitRuns_KeepsReadingTheSameRows()
	{
		var media = new MediaRow();
		fixture.Context.ExternalMediaIds.Add(TmdbId.From("550", MediaType.Movie).ToExternalIdEntity(media));
		await fixture.Context.SaveChangesAsync();
		await using var reading = await fixture.ContextFactory.CreateDbContextAsync();
		await using var snapshot = await reading.SnapshotAsync(CancellationToken.None);
		var before = await reading.MediaRatings.CountAsync();

		var repository = new MediaRepository(fixture.ContextFactory, new MediaDetailsFactory(), new MediaWriteLock(), fixture.Resolve<IDateTimeProvider>());

		await repository.SaveDetailsAsync([new DetailsChange(MediaId.From(media.Id), new None(), Rated())]);

		await Assert.That(await reading.MediaRatings.CountAsync()).IsEqualTo(before);
	}

	private static CachedDetails Rated()
	{
		var at = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
		return new CachedDetails(new MovieDetails { Title = "Fight Club", Ratings = MediaRatings.From([new TmdbRating(8.4f, Some.Of(1000))]) }, at, at, []);
	}
}
