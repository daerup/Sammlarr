using System.ComponentModel;
using Contracts.Processing;
using Core.Feed;
using Domain.Media;
using Domain.Processing;
using TestInfrastructure;

namespace Core.Tests.Feed;

public class FeedNeedsTests
{
	private static readonly ValueSet<RatingSource> Available = [RatingSource.Imdb, RatingSource.Tmdb];

	[Test]
	public async Task Needs_ForARatingFilterOnFirstAvailable_IsEveryAvailableSource()
	{
		var feed = TestFeeds.Feed(Guid.NewGuid()) with { Filter = Some.Of(Filtering(nameof(MediaQueryRow.Rating))) };

		var needs = feed.Needs(new OrderedRating([RatingSource.Metacritic, RatingSource.Imdb, RatingSource.Tmdb]), Available);

		await Assert.That(needs.Required).IsEqualTo(MediaFields.Rated([RatingSource.Imdb, RatingSource.Tmdb]));
	}

	[Test]
	public async Task Needs_ForASortOnRuntime_IsBothRuntimes()
	{
		var feed = TestFeeds.Feed(Guid.NewGuid()) with
		{
			Sort = Some.Of(new QuerySort { Property = nameof(MediaQueryRow.RuntimeMinutes), Direction = ListSortDirection.Descending }),
		};

		var needs = feed.Needs(AveragedRating.Everything, Available);

		await Assert.That(needs.Required).IsEqualTo(MediaField.Runtime | MediaField.RuntimePerEpisode);
	}

	[Test]
	public async Task Needs_ForAFilterOnTotalRuntime_IsBothRuntimesAndTheEpisodeCount()
	{
		var feed = TestFeeds.Feed(Guid.NewGuid()) with { Filter = Some.Of(Filtering(nameof(MediaQueryRow.TotalRuntimeMinutes))) };

		var needs = feed.Needs(AveragedRating.Everything, Available);

		await Assert.That(needs.Required).IsEqualTo(MediaField.Runtime | MediaField.RuntimePerEpisode | MediaField.Episodes);
	}

	[Test]
	public async Task Needs_WithoutFilterOrSort_IsWhatACardShows()
	{
		var needs = TestFeeds.Feed(Guid.NewGuid()).Needs(AveragedRating.Everything, Available);

		await Assert.That(needs).IsEqualTo(new FieldNeeds(MediaFields.None, FeedNeeds.Shown));
		await Assert
			.That(FeedNeeds.Shown)
			.IsEqualTo(MediaField.Title | MediaField.ReleaseDate | MediaField.Poster | MediaField.Backdrop | MediaField.Summary | MediaFields.AllRatings);
	}

	private static QueryFilter Filtering(string field)
	{
		return new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups =
			[
				new FilterGroup
				{
					Operator = FilterOperator.And,
					Predicates =
					[
						new FilterPredicate
						{
							Field = field,
							Operator = FilterPredicateOperator.GreaterThan,
							Value = "7",
						},
					],
				},
			],
		};
	}
}
