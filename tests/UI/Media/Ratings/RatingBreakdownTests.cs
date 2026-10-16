using Domain.Media;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Shared.Media.Ratings;

namespace UI.Tests.Media.Ratings;

public class RatingBreakdownTests
{
	private static readonly ValueSet<RatingSource> Everything =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
	];

	private static readonly MediaRatings ProjectHailMary = MediaRatings.From([
		new TmdbRating(8.6f, Some.Of(7705)),
		new ImdbRating(8.2f, Some.Of(542195)),
		new RottenTomatoesCriticsRating(95),
		new RottenTomatoesAudienceRating(95),
	]);

	[Test]
	public async Task Of_WhenAveragingEverySource_CountsEveryRowWithoutReasonsOrRanks()
	{
		var breakdown = RatingBreakdown.Of(ProjectHailMary, new RatingStrategy(AveragedRating.Everything), Everything);

		await Assert.That(breakdown.Rows.Select(row => row.State)).IsEquivalentTo(Enumerable.Repeat(RatingRowState.Counts, 4));
		await Assert.That(breakdown.Rows.Any(row => row.SaysWhy || row.Rank.IsSome())).IsFalse();
		await Assert.That(breakdown.Ranked).IsFalse();
	}

	[Test]
	public async Task Of_WhenAverageLeavesSourcesOut_DimsThemAndSaysWhyOnce()
	{
		var breakdown = RatingBreakdown.Of(
			ProjectHailMary,
			new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])),
			Everything
		);

		await Assert
			.That(breakdown.Rows.Select(row => (row.Source, row.State, row.SaysWhy)))
			.IsEquivalentTo(
				[
					(RatingSource.Tmdb, RatingRowState.SwitchedOff, true),
					(RatingSource.Imdb, RatingRowState.Counts, false),
					(RatingSource.RottenTomatoesCritics, RatingRowState.Counts, false),
					(RatingSource.RottenTomatoesAudience, RatingRowState.SwitchedOff, false),
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Of_WhenFirstAvailable_RanksEveryRowAndMarksTheLaterOnesNotNeeded()
	{
		var breakdown = RatingBreakdown.Of(ProjectHailMary, new RatingStrategy(new OrderedRating([RatingSource.Imdb])), Everything);

		await Assert.That(breakdown.Ranked).IsTrue();
		await Assert
			.That(breakdown.Rows.Select(row => (row.Source, row.State, row.SaysWhy, row.Rank.GetValueOr(0))))
			.IsEquivalentTo(
				[
					(RatingSource.Imdb, RatingRowState.Counts, false, 1),
					(RatingSource.Tmdb, RatingRowState.NotNeeded, true, 2),
					(RatingSource.RottenTomatoesCritics, RatingRowState.NotNeeded, false, 3),
					(RatingSource.RottenTomatoesAudience, RatingRowState.NotNeeded, false, 4),
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Of_WhenAKeptChoiceNeedsAProvider_LeavesItOutAndUsesTheRestInstead()
	{
		var breakdown = RatingBreakdown.Of(
			ProjectHailMary,
			new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])),
			[RatingSource.Tmdb]
		);

		await Assert
			.That(breakdown.Rows.Select(row => (row.Source, row.State, row.SaysWhy)))
			.IsEquivalentTo([(RatingSource.Tmdb, RatingRowState.Counts, false)], CollectionOrdering.Matching);
		await Assert.That(breakdown.UsedInstead).IsTrue();
	}

	[Test]
	public async Task Of_WhenNoSourceRatesTheTitle_ListsNoRow()
	{
		var averaged = RatingBreakdown.Of(MediaRatings.None, new RatingStrategy(AveragedRating.Everything), Everything);
		var ordered = RatingBreakdown.Of(MediaRatings.None, new RatingStrategy(new OrderedRating()), Everything);

		await Assert.That(averaged.Sammlarr).IsNone();
		await Assert.That(averaged.Rows).IsEmpty();
		await Assert.That(ordered.Rows).IsEmpty();
	}

	[Test]
	public async Task Of_WhenFirstAvailable_KeepsTheSkippedEmptyRowsAndHidesTheLaterOnes()
	{
		var ratings = MediaRatings.From([new ImdbRating(8.2f, Some.Of(542195)), new RottenTomatoesCriticsRating(95)]);
		var order = new OrderedRating([RatingSource.RottenTomatoesAudience, RatingSource.Tmdb, RatingSource.Imdb, RatingSource.RottenTomatoesCritics]);

		var breakdown = RatingBreakdown.Of(ratings, new RatingStrategy(order), Everything);

		await Assert
			.That(breakdown.Rows.Select(row => (row.Source, row.State, row.SaysWhy)))
			.IsEquivalentTo(
				[
					(RatingSource.RottenTomatoesAudience, RatingRowState.Skipped, true),
					(RatingSource.Tmdb, RatingRowState.Skipped, true),
					(RatingSource.Imdb, RatingRowState.Counts, false),
					(RatingSource.RottenTomatoesCritics, RatingRowState.NotNeeded, true),
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Of_WhenFirstAvailableHidesAnEmptyRow_NumbersTheShownRows()
	{
		var ratings = MediaRatings.From([new ImdbRating(8.2f, Some.Of(542195)), new RottenTomatoesCriticsRating(95)]);
		var order = new OrderedRating([RatingSource.Imdb, RatingSource.Tmdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience]);

		var breakdown = RatingBreakdown.Of(ratings, new RatingStrategy(order), Everything);

		await Assert
			.That(breakdown.Rows.Select(row => (row.Source, row.Rank.GetValueOr(0))))
			.IsEquivalentTo([(RatingSource.Imdb, 1), (RatingSource.RottenTomatoesCritics, 2)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Of_WhenAveragingAndASwitchedOffSourceHasNoRating_LeavesItOut()
	{
		var ratings = MediaRatings.From([new ImdbRating(8.2f, Some.Of(542195)), new RottenTomatoesCriticsRating(95)]);

		var breakdown = RatingBreakdown.Of(ratings, new RatingStrategy(new AveragedRating([RatingSource.Imdb])), Everything);

		await Assert
			.That(breakdown.Rows.Select(row => (row.Source, row.State, row.SaysWhy)))
			.IsEquivalentTo(
				[(RatingSource.Imdb, RatingRowState.Counts, false), (RatingSource.RottenTomatoesCritics, RatingRowState.SwitchedOff, true)],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Of_WhenAProviderIsNotConnected_ListsNoRowForItsSources()
	{
		var breakdown = RatingBreakdown.Of(ProjectHailMary, new RatingStrategy(AveragedRating.Everything), [RatingSource.Tmdb]);

		await Assert.That(breakdown.Rows.Select(row => row.Source)).IsEquivalentTo([RatingSource.Tmdb]);
		await Assert.That(breakdown.UsedInstead).IsFalse();
	}

	[Test]
	public async Task Of_WhenOnlyStoredRatingsAreKnown_ListsTheRatedSourcesAndSaysTheRestMayFollow()
	{
		var breakdown = RatingBreakdown.Of(
			MediaRatings.From([new TmdbRating(9.0f, Some.Of(8))]),
			new RatingStrategy(AveragedRating.Everything),
			Everything,
			storedOnly: true
		);

		await Assert.That(breakdown.Rows.Select(row => row.Source)).IsEquivalentTo([RatingSource.Tmdb]);
		await Assert.That(breakdown.Partial).IsTrue();

		var ranked = RatingBreakdown.Of(
			MediaRatings.From([new ImdbRating(8.2f, Some.Of(542195))]),
			new RatingStrategy(new OrderedRating([RatingSource.Tmdb, RatingSource.Imdb])),
			Everything,
			storedOnly: true
		);

		await Assert.That(ranked.Rows.Select(row => (row.Source, row.State))).IsEquivalentTo([(RatingSource.Imdb, RatingRowState.Counts)]);
		await Assert.That(ranked.Partial).IsTrue();
	}

	[Test]
	public async Task Of_WhenAveragingAndOneSourceHasNoRating_LeavesItOutAndAveragesTheRest()
	{
		var lastSeen = MediaRatings.From([new TmdbRating(9.0f, Some.Of(8)), new ImdbRating(7.6f, Some.Of(303)), new RottenTomatoesCriticsRating(87)]);

		var breakdown = RatingBreakdown.Of(lastSeen, new RatingStrategy(AveragedRating.Everything), Everything);

		await Assert.That(breakdown.Rows.Select(row => row.Source)).DoesNotContain(RatingSource.RottenTomatoesAudience);
		await Assert.That(breakdown.Sammlarr.Map(rating => rating.Counted.Count)).IsSome().And.IsEqualTo(3);
		await Assert.That(breakdown.Partial).IsFalse();
	}
}
