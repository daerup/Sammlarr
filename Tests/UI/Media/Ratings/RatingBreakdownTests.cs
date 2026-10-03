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

	private static readonly RatingSummary ProjectHailMary = RatingSummary.From([
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
	public async Task Of_WhenAKeptChoiceNeedsAProvider_PutsItsSourcesFirstAndUsesTheRestInstead()
	{
		var breakdown = RatingBreakdown.Of(
			ProjectHailMary,
			new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])),
			[RatingSource.Tmdb]
		);

		await Assert
			.That(breakdown.Rows.Select(row => (row.Source, row.State, row.SaysWhy)))
			.IsEquivalentTo(
				[
					(RatingSource.Imdb, RatingRowState.NeedsProvider, true),
					(RatingSource.RottenTomatoesCritics, RatingRowState.NeedsProvider, true),
					(RatingSource.Tmdb, RatingRowState.Counts, false),
				],
				CollectionOrdering.Matching
			);
		await Assert.That(breakdown.UsedInstead).IsTrue();
		await Assert.That(breakdown.Rows.Where(row => row.State == RatingRowState.NeedsProvider).All(row => row.Rating.IsNone())).IsTrue();
	}

	[Test]
	public async Task Of_WhenNoSourceRatesTheTitle_DimsEveryRowWithoutRepeatingWhy()
	{
		var breakdown = RatingBreakdown.Of(RatingSummary.None, new RatingStrategy(AveragedRating.Everything), Everything);

		await Assert.That(breakdown.Sammlarr).IsNone();
		await Assert.That(breakdown.Rows.All(row => row.State == RatingRowState.NoRating && !row.SaysWhy)).IsTrue();
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
			RatingSummary.From([new TmdbRating(9.0f, Some.Of(8))]),
			new RatingStrategy(AveragedRating.Everything),
			Everything,
			storedOnly: true
		);

		await Assert.That(breakdown.Rows.Select(row => row.Source)).IsEquivalentTo([RatingSource.Tmdb]);
		await Assert.That(breakdown.Partial).IsTrue();
	}

	[Test]
	public async Task Of_WhenOneSourceHasNoRating_SaysSoOnItsRowAndAveragesTheRest()
	{
		var lastSeen = RatingSummary.From([new TmdbRating(9.0f, Some.Of(8)), new ImdbRating(7.6f, Some.Of(303)), new RottenTomatoesCriticsRating(87)]);

		var breakdown = RatingBreakdown.Of(lastSeen, new RatingStrategy(AveragedRating.Everything), Everything);

		var audience = breakdown.Rows.Single(row => row.Source == RatingSource.RottenTomatoesAudience);
		await Assert.That(audience.State).IsEqualTo(RatingRowState.NoRating);
		await Assert.That(audience.SaysWhy).IsTrue();
		await Assert.That(breakdown.Sammlarr.Map(rating => rating.From.Count)).IsSome().And.IsEqualTo(3);
	}
}
