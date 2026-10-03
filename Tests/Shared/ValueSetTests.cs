using Domain.Media;

namespace Shared.Tests;

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
	public async Task GetHashCode_WhenAnItemHashesToZero_StillTellsTheSetsApart()
	{
		ValueSet<RatingSource> withTmdb = [RatingSource.Tmdb, RatingSource.Imdb];
		ValueSet<RatingSource> without = [RatingSource.Imdb];

		await Assert.That(withTmdb.GetHashCode()).IsNotEqualTo(without.GetHashCode());
		await Assert.That(new ValueSet<RatingSource>().GetHashCode()).IsNotEqualTo(((ValueSet<RatingSource>)[RatingSource.Tmdb]).GetHashCode());
	}

	[Test]
	public async Task GetHashCode_WhenSameItemsDifferentOrder_IsTheSame()
	{
		ValueSet<RatingSource> first = [RatingSource.Tmdb, RatingSource.RottenTomatoesAudience];
		ValueSet<RatingSource> second = [RatingSource.RottenTomatoesAudience, RatingSource.Tmdb];

		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task Equals_WhenDifferentItems_AreNotEqual()
	{
		ValueSet<IExternalMediaId> first = [TmdbId.From("1", MediaType.Movie)];
		ValueSet<IExternalMediaId> second = [TmdbId.From("2", MediaType.Movie)];

		await Assert.That(first).IsNotEqualTo(second);
	}
}
