using BlazorBlueprint.Components;
using Bunit;
using Domain.Media;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Shared.Media.Ratings;

namespace UI.Tests.Media.Ratings;

public class RatingSumTests : BunitContext
{
	private static readonly ValueSet<RatingSource> Everything = [.. Enum.GetValues<RatingSource>()];

	private static readonly MediaRatings Digger = MediaRatings.From([
		new ImdbRating(7.3f, Some.Of(6382)),
		new RottenTomatoesAudienceRating(71),
		new RottenTomatoesCriticsRating(51),
		new TmdbRating(7.4f, Some.Of(109)),
	]);

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
	}

	[Test]
	public async Task Terms_ForDiggersAverage_ListTheRowsOrder()
	{
		var rating = await AverageAsync(Digger);

		await Assert
			.That(RatingTexts.Terms(rating).Select(term => (term.Rating.Source, term.Number)))
			.IsEquivalentTo(
				[(RatingSource.Tmdb, "7.4"), (RatingSource.Imdb, "7.3"), (RatingSource.RottenTomatoesCritics, "5.1"), (RatingSource.RottenTomatoesAudience, "7.1")],
				CollectionOrdering.Matching
			);
		await Assert.That(RatingTexts.Number(rating)).IsEqualTo("6.7");
	}

	[Test]
	public async Task VoteTerms_ForDiggersAverage_ListTheVotesInTheRowsOrder()
	{
		var rating = await AverageAsync(Digger);

		await Assert.That(RatingTexts.VoteTerms(rating).Select(term => term.Number)).IsEquivalentTo(["109", "6,382"], CollectionOrdering.Matching);
		await Assert.That(rating.Votes).IsSome().And.IsEqualTo(6491);
	}

	[Test]
	public async Task Sum_ForSeveralRatings_ReadsAsOneLineEndingOnTheHeadingsNumber()
	{
		var rating = await AverageAsync(Digger);

		var cut = Render<RatingSum>(parameters => parameters.Add(sum => sum.Terms, RatingTexts.Terms(rating)).Add(sum => sum.Result, RatingTexts.Number(rating)));

		await Assert.That(Squeezed(cut.Find(".rating-sum").TextContent)).IsEqualTo("(7.4+7.3+5.1+7.1)÷4=6.7");
		await Assert.That(cut.FindAll(".rating-sum-term").Count).IsEqualTo(4);
	}

	[Test]
	public async Task Sum_ForVotes_AddsThemUpWithoutDividing()
	{
		var rating = await AverageAsync(Digger);

		var cut = Render<RatingSum>(parameters =>
			parameters.Add(sum => sum.Terms, RatingTexts.VoteTerms(rating)).Add(sum => sum.Result, "6,491").Add(sum => sum.Divided, false)
		);

		await Assert.That(Squeezed(cut.Find(".rating-sum").TextContent)).IsEqualTo("109+6,382=6,491");
	}

	[Test]
	public async Task Sum_ForASingleRating_KeepsItsSentence()
	{
		var rating = await Assert.That(new OrderedRating().Resolve(Digger, Everything)).IsSome();

		var cut = Render<RatingSum>(parameters =>
			parameters
				.Add(sum => sum.Terms, RatingTexts.Terms(rating))
				.Add(sum => sum.Result, RatingTexts.Number(rating))
				.Add(sum => sum.Why, RatingTexts.Why(new RatingStrategy(new OrderedRating())))
		);

		await Assert.That(Squeezed(cut.Find(".rating-sum").TextContent)).IsEqualTo("7.4→7.4,firstintheorder");
	}

	private static async Task<SammlarrRating> AverageAsync(MediaRatings ratings)
	{
		return await Assert.That(AveragedRating.Everything.Resolve(ratings, Everything)).IsSome();
	}

	private static string Squeezed(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));
}
