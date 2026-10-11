using Contracts.Processing;
using Domain.Media;
using Domain.Processing;

namespace Core.Feed;

internal static class FeedNeeds
{
	internal static MediaFields Shown { get; } =
		(MediaField.Title | MediaField.ReleaseDate | MediaField.Poster | MediaField.Backdrop | MediaField.Summary) | MediaFields.AllRatings;

	extension(Domain.Feed.Feed feed)
	{
		internal FieldNeeds Needs(RatingStrategy strategy, ValueSet<RatingSource> available)
		{
			var read = feed.Filter.Map(Fields).GetValueOr([]).Concat(feed.Sort.Map(sort => new[] { sort.Property }).GetValueOr([]));
			return new FieldNeeds(read.Aggregate(MediaFields.None, (needs, field) => needs | Of(field, strategy, available)), Shown);
		}
	}

	private static IEnumerable<string> Fields(QueryFilter filter) => filter.Groups.SelectMany(Fields);

	private static IEnumerable<string> Fields(FilterGroup group) => group.Predicates.Select(predicate => predicate.Field).Concat(group.Groups.SelectMany(Fields));

	private static MediaFields Of(string field, RatingStrategy strategy, ValueSet<RatingSource> available)
	{
		return field switch
		{
			nameof(MediaQueryRow.Rating) or nameof(MediaQueryRow.Votes) => MediaFields.Rated(Sources(strategy, available)),
			nameof(MediaQueryRow.Title) => MediaField.Title,
			nameof(MediaQueryRow.Tagline) => MediaField.Tagline,
			nameof(MediaQueryRow.Summary) => MediaField.Summary,
			nameof(MediaQueryRow.Genres) => MediaField.Genres,
			nameof(MediaQueryRow.ReleaseDate) => MediaField.ReleaseDate,
			nameof(MediaQueryRow.RuntimeMinutes) => MediaField.Runtime | MediaField.RuntimePerEpisode,
			nameof(MediaQueryRow.Seasons) => MediaField.Seasons,
			nameof(MediaQueryRow.Episodes) => MediaField.Episodes,
			_ => MediaFields.None,
		};
	}

	private static IEnumerable<RatingSource> Sources(RatingStrategy strategy, ValueSet<RatingSource> available)
	{
		return strategy switch
		{
			OrderedRating ordered => ordered.Order.Where(available.Contains),
			AveragedRating averaged => averaged.Counted(available),
		};
	}
}
