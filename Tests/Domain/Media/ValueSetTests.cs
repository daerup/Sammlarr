using Domain.Media;

namespace Domain.Tests.Media;

public class ValueSetTests
{
	[Test]
	public async Task Equals_WhenSameItemsDifferentOrder_AreEqual()
	{
		IExternalMediaId tmdb = TmdbId.From("1", MediaType.Movie);
		IExternalMediaId imdb = ImdbId.From("tt2", MediaType.Movie);
		ValueSet<IExternalMediaId> first = [tmdb, imdb];
		ValueSet<IExternalMediaId> second = [imdb, tmdb];

		await Assert.That(first).IsEqualTo(second);
	}

	[Test]
	public async Task Create_WhenDuplicateItems_Dedupes()
	{
		var tmdb = TmdbId.From("1", MediaType.Movie);
		ValueSet<IExternalMediaId> set = [tmdb, tmdb];

		await Assert.That(set.Count).IsEqualTo(1);
	}

	[Test]
	public async Task Equals_WhenDifferentItems_AreNotEqual()
	{
		ValueSet<IExternalMediaId> first = [TmdbId.From("1", MediaType.Movie)];
		ValueSet<IExternalMediaId> second = [TmdbId.From("2", MediaType.Movie)];

		await Assert.That(first).IsNotEqualTo(second);
	}
}
