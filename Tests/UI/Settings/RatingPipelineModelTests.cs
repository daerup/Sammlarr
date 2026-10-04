using Domain.Media;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Ratings;

namespace UI.Tests.Settings;

public class RatingPipelineModelTests
{
	private static readonly ValueSet<RatingSource> Everything =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
	];

	[Test]
	public async Task For_WhenAveragingSomeSources_SinksTheSwitchedOffOnesWithoutATrace()
	{
		var model = RatingPipelineModel.For(new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])), Everything);

		await Assert
			.That(model.Tiles.Select(tile => (tile.Source, tile.On)))
			.IsEquivalentTo(
				[(RatingSource.Imdb, true), (RatingSource.RottenTomatoesCritics, true), (RatingSource.Tmdb, false), (RatingSource.RottenTomatoesAudience, false)],
				CollectionOrdering.Matching
			);
		await Assert.That(model.Traces.Select(trace => trace.Source)).IsEquivalentTo([RatingSource.Imdb, RatingSource.RottenTomatoesCritics]);
		await Assert.That(model.Dashed).IsFalse();
	}

	[Test]
	public async Task For_WhenNoneOfTheAveragedSourcesIsAvailable_CountsTheRestOnDashedTraces()
	{
		var model = RatingPipelineModel.For(new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])), [RatingSource.Tmdb]);

		await Assert.That(model.Tiles.Select(tile => (tile.Source, tile.On))).IsEquivalentTo([(RatingSource.Tmdb, false)]);
		await Assert.That(model.Traces.Select(trace => trace.Source)).IsEquivalentTo([RatingSource.Tmdb]);
		await Assert.That(model.Dashed).IsTrue();
	}

	[Test]
	public async Task For_WhenFirstAvailable_RanksEveryAvailableSourceAndFeedsThemAll()
	{
		var model = RatingPipelineModel.For(new RatingStrategy(new OrderedRating([RatingSource.Imdb])), [RatingSource.Tmdb, RatingSource.Imdb]);

		await Assert
			.That(model.Tiles.Select(tile => (tile.Source, tile.Rank.GetValueOr(0))))
			.IsEquivalentTo([(RatingSource.Imdb, 1), (RatingSource.Tmdb, 2)], CollectionOrdering.Matching);
		await Assert.That(model.Traces.Count).IsEqualTo(2);
	}

	[Test]
	public async Task For_WhenSeveralSourcesCount_CentresTheirPinsAndTheRatingOnThem()
	{
		var model = RatingPipelineModel.For(new RatingStrategy(AveragedRating.Everything), Everything);
		var middle = model.Tiles.Average(tile => tile.Y);

		await Assert.That(model.Traces.Average(trace => trace.Pin)).IsEqualTo(middle).Within(0.001);
		await Assert.That(model.OutputY + RatingPipelineModel.OutputHeight / 2).IsEqualTo(middle).Within(0.001);
		await Assert.That(model.Traces.Select(trace => trace.Pin)).IsInOrder();
	}

	[Test]
	public async Task For_WhenOneSourceCounts_FitsTheRatingInsideTheCard()
	{
		var model = RatingPipelineModel.For(new RatingStrategy(AveragedRating.Everything), [RatingSource.Tmdb]);

		await Assert.That(model.OutputY).IsGreaterThanOrEqualTo(0);
		await Assert.That(model.Height).IsGreaterThanOrEqualTo(RatingPipelineModel.OutputHeight);
		await Assert.That(model.Traces.Single().Pin).IsEqualTo(model.Tiles.Single().Y);
	}
}
