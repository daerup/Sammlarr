using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class MediaRatingTests
{
	[Test]
	public async Task TmdbRating_ReadsAsTheAverageOnTheSammlarrScale()
	{
		var rating = new TmdbRating(8.64f, Some.Of(7705));

		await Assert.That(rating.Value).IsEqualTo("8.6");
		await Assert.That(rating.SammlarrValue).IsEqualTo(8.64f);
		await Assert.That(rating.Votes).IsSome().And.IsEqualTo(7705);
		await Assert.That(rating.Mark).IsEqualTo(RatingMark.Tmdb);
	}

	[Test]
	public async Task RottenTomatoesCriticsRating_CountsItsPercentAsATenth()
	{
		var rating = new RottenTomatoesCriticsRating(95);

		await Assert.That(rating.Value).IsEqualTo("95%");
		await Assert.That(rating.SammlarrValue).IsEqualTo(9.5f);
		await Assert.That(rating.Votes).IsNone();
	}

	[Test]
	public async Task ImdbRating_ReadsAsTheAverageOnTheSammlarrScale()
	{
		var rating = new ImdbRating(8.2f, Some.Of(542195));

		await Assert.That(rating.Value).IsEqualTo("8.2");
		await Assert.That(rating.SammlarrValue).IsEqualTo(8.2f);
		await Assert.That(rating.Mark).IsEqualTo(RatingMark.Imdb);
	}

	[Test]
	public async Task RottenTomatoesAudienceRating_CountsItsPercentAsATenth()
	{
		var rating = new RottenTomatoesAudienceRating(88);

		await Assert.That(rating.Value).IsEqualTo("88%");
		await Assert.That(rating.SammlarrValue).IsEqualTo(8.8f);
		await Assert.That(rating.Votes).IsNone();
	}

	[Test]
	[Arguments(64, 6.4f)]
	[Arguments(72, 7.2f)]
	[Arguments(96, 9.6f)]
	public async Task RottenTomatoesCriticsRating_ComparesLikeTheSameNumberFromAnotherSource(int percent, float tenth)
	{
		await Assert.That(new RottenTomatoesCriticsRating(percent).SammlarrValue).IsEqualTo(new TmdbRating(tenth, new None()).SammlarrValue);
	}

	[Test]
	[Arguments(59, RatingMark.Rotten)]
	[Arguments(60, RatingMark.Fresh)]
	public async Task RottenTomatoesCriticsRating_IsFreshFromSixtyPercent(int percent, RatingMark mark)
	{
		await Assert.That(new RottenTomatoesCriticsRating(percent).Mark).IsEqualTo(mark);
	}

	[Test]
	[Arguments(59, RatingMark.Spilled)]
	[Arguments(60, RatingMark.Upright)]
	public async Task RottenTomatoesAudienceRating_IsUprightFromSixtyPercent(int percent, RatingMark mark)
	{
		await Assert.That(new RottenTomatoesAudienceRating(percent).Mark).IsEqualTo(mark);
	}

	[Test]
	public async Task Rate_ForEachSource_RebuildsTheRatingFromItsSammlarrValue()
	{
		IMediaRating[] ratings =
		[
			new TmdbRating(8.6f, Some.Of(7705)),
			new ImdbRating(8.2f, Some.Of(542_195)),
			new RottenTomatoesCriticsRating(36),
			new RottenTomatoesAudienceRating(93),
		];

		await Assert.That(ratings.Select(rating => rating.Source.Rate(rating.SammlarrValue, rating.Votes))).IsEquivalentTo(ratings);
	}

	[Test]
	public async Task SuppliedBy_NamesEveryProviderThatSuppliesTheSource()
	{
		await Assert.That(RatingSource.Tmdb.SuppliedBy()).IsEquivalentTo([MediaProvider.Tmdb, MediaProvider.Plex]);
		await Assert.That(RatingSource.RottenTomatoesAudience.SuppliedBy()).IsEquivalentTo([MediaProvider.Plex]);
	}

	[Test]
	public async Task SuppliedRatingSources_ForTheConnectedProviders_AreTheSourcesAnyOfThemSupplies()
	{
		await Assert.That(MediaProvider.Plex.SuppliedRatingSources()).IsEquivalentTo(Enum.GetValues<RatingSource>());
		await Assert.That(MediaProvider.Mdblist.SuppliedRatingSources()).IsEmpty();
		await Assert.That(new[] { MediaProvider.Tmdb, MediaProvider.Mdblist }.SuppliedRatingSources()).IsEquivalentTo([RatingSource.Tmdb]);
	}
}
