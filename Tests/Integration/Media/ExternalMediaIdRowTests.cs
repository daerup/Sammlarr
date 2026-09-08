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
	public async Task ExternalId_PersistsAndReloadsAllThreeFields()
	{
		var media = new MediaRow();
		fixture.Context.ExternalMediaIds.Add(
			new ExternalMediaIdRow
			{
				Source = MediaProvider.Tmdb,
				MediaType = MediaType.Movie,
				Id = "12345",
				Media = media,
			}
		);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		var reloaded = await fixture.Context.ExternalMediaIds.AsNoTracking().SingleAsync();

		using var _ = Assert.Multiple();
		await Assert.That(reloaded.Source).IsEqualTo(MediaProvider.Tmdb);
		await Assert.That(reloaded.MediaType).IsEqualTo(MediaType.Movie);
		await Assert.That(reloaded.Id).IsEqualTo("12345");
	}

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
	public async Task WhereMatchingAny_TranslatesAtScale()
	{
		const int count = 2000;
		var ids = Enumerable.Range(0, count).Select(i => (IExternalMediaId)TvdbId.From($"tvdb-{i}", MediaType.Movie)).ToList();
		var media = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		var matched = await fixture.Context.ExternalMediaIds.AsNoTracking().WhereMatchingAny(ids).ToListAsync();

		await Assert.That(matched).Count().IsEqualTo(count);
	}
}
