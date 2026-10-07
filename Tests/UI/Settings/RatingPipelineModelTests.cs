using CsCheck;
using Domain.Media;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Ratings;

namespace UI.Tests.Settings;

public class RatingPipelineModelTests
{
	private static readonly RatingSource[] Sources = Enum.GetValues<RatingSource>();

	private static readonly ValueSet<RatingSource> Everything = [.. Sources];

	private static readonly OrderedRating SourceOrder = new();

	private static readonly Gen<ValueSet<RatingSource>> Sets = Gen
		.Bool.Array[Sources.Length]
		.Select(flags => (ValueSet<RatingSource>)[.. Sources.Where((_, index) => flags[index])]);

	private static readonly Gen<OrderedRating> Orders = Gen.Shuffle(Sources).Select(order => new OrderedRating(order));

	[Test]
	public async Task For_WhenAveragingSomeSources_KeepsTheSwitchedOffOnesInPlaceWithoutATrace()
	{
		var model = RatingPipelineModel.For(
			new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])),
			SourceOrder,
			Everything
		);

		await Assert
			.That(model.Tiles.Select(tile => (tile.Source, tile.On)))
			.IsEquivalentTo(
				[
					(RatingSource.Tmdb, false),
					(RatingSource.Imdb, true),
					(RatingSource.RottenTomatoesCritics, true),
					(RatingSource.RottenTomatoesAudience, false),
					(RatingSource.Metacritic, false),
					(RatingSource.Letterboxd, false),
					(RatingSource.Trakt, false),
					(RatingSource.MyAnimeList, false),
					(RatingSource.AniList, false),
				],
				CollectionOrdering.Matching
			);
		await Assert.That(model.Traces.Select(trace => trace.Source)).IsEquivalentTo([RatingSource.Imdb, RatingSource.RottenTomatoesCritics]);
		await Assert.That(model.Dashed).IsFalse();
	}

	[Test]
	public void For_WhenAveraging_PlacesEveryRowWhereFirstAvailableDoesWhateverIsSwitchedOn()
	{
		Gen.Select(Sets, Orders, Sets).Sample(PlacesRowsAsFirstAvailable);
	}

	[Test]
	public void For_WhenAveraging_CentresTheRatingOnEveryRow()
	{
		Gen.Select(Sets, Orders, Sets.Where(available => available.Count > 0)).Sample(CentresOnEveryRow);
	}

	[Test]
	public async Task For_WhenNoneOfTheAveragedSourcesIsAvailable_CountsTheRestOnDashedTraces()
	{
		var model = RatingPipelineModel.For(
			new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])),
			SourceOrder,
			[RatingSource.Tmdb]
		);

		await Assert.That(model.Tiles.Select(tile => (tile.Source, tile.On))).IsEquivalentTo([(RatingSource.Tmdb, false)]);
		await Assert.That(model.Traces.Select(trace => trace.Source)).IsEquivalentTo([RatingSource.Tmdb]);
		await Assert.That(model.Dashed).IsTrue();
	}

	[Test]
	public async Task For_WhenFirstAvailable_RanksEveryAvailableSourceAndFeedsThemAll()
	{
		var ordered = new OrderedRating([RatingSource.Imdb]);
		var model = RatingPipelineModel.For(new RatingStrategy(ordered), ordered, [RatingSource.Tmdb, RatingSource.Imdb]);

		await Assert
			.That(model.Tiles.Select(tile => (tile.Source, tile.Rank.GetValueOr(0))))
			.IsEquivalentTo([(RatingSource.Imdb, 1), (RatingSource.Tmdb, 2)], CollectionOrdering.Matching);
		await Assert.That(model.Traces.Count).IsEqualTo(2);
	}

	[Test]
	public async Task For_WhenSeveralSourcesCount_CentresTheirPinsAndTheRatingOnThem()
	{
		var model = RatingPipelineModel.For(new RatingStrategy(AveragedRating.Everything), SourceOrder, Everything);
		var middle = model.Tiles.Average(tile => tile.Y);

		await Assert.That(model.Traces.Average(trace => trace.Pin)).IsEqualTo(middle).Within(0.001);
		await Assert.That(model.OutputY + RatingPipelineModel.OutputHeight / 2).IsEqualTo(middle).Within(0.001);
		await Assert.That(model.Traces.Select(trace => trace.Pin)).IsInOrder();
	}

	[Test]
	public async Task For_WhenOneSourceCounts_FitsTheRatingInsideTheCard()
	{
		var model = RatingPipelineModel.For(new RatingStrategy(AveragedRating.Everything), SourceOrder, [RatingSource.Tmdb]);

		await Assert.That(model.OutputY).IsGreaterThanOrEqualTo(0);
		await Assert.That(model.Height).IsGreaterThanOrEqualTo(RatingPipelineModel.OutputHeight);
		await Assert.That(model.Traces.Single().Pin).IsEqualTo(model.Tiles.Single().Y);
	}

	private static bool PlacesRowsAsFirstAvailable(ValueSet<RatingSource> switchedOn, OrderedRating order, ValueSet<RatingSource> available)
	{
		var averaged = RatingPipelineModel.For(new RatingStrategy(new AveragedRating(switchedOn)), order, available);
		var ranked = RatingPipelineModel.For(new RatingStrategy(order), order, available);
		return averaged.Tiles.Select(tile => (tile.Source, tile.Y)).SequenceEqual(ranked.Tiles.Select(tile => (tile.Source, tile.Y)));
	}

	private static bool CentresOnEveryRow(ValueSet<RatingSource> switchedOn, OrderedRating order, ValueSet<RatingSource> available)
	{
		var model = RatingPipelineModel.For(new RatingStrategy(new AveragedRating(switchedOn)), order, available);
		return Math.Abs(model.OutputY + RatingPipelineModel.OutputHeight / 2 - model.Tiles.Average(tile => tile.Y)) < 0.001;
	}
}
