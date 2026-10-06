using System.Globalization;
using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Media;
using UI.Shared.Media.Ratings;
using FilterOperator = BlazorBlueprint.Primitives.Filtering.FilterOperator;

namespace UI.Features.Feeds.Pages.Editor;

internal static class FeedRatingReasons
{
	private const string Unrated = "no rating yet; no provider rates it";

	public static IReadOnlyList<string> For(FilterDefinition filter, Optional<SammlarrRating> rating, RatingStrategy strategy)
	{
		return [.. Conditions(filter).Select(condition => Reason(condition, rating, strategy)).WhereSome()];
	}

	private static IEnumerable<FilterCondition> Conditions(FilterDefinition filter) => filter.Conditions.Concat(filter.Groups.SelectMany(Conditions));

	private static Optional<string> Reason(FilterCondition condition, Optional<SammlarrRating> rating, RatingStrategy strategy)
	{
		return condition.Field switch
		{
			nameof(MediaQueryRow.Rating) => Optional.When(
				!condition.MatchesValue(rating.Map(found => found.Rounded).ToNullable(), FilterFieldType.Number),
				() => RatingReason(condition, rating, strategy)
			),
			nameof(MediaQueryRow.Votes) => Optional.When(
				!condition.MatchesValue(rating.Map(found => found.Votes).Map(votes => (double)votes).ToNullable(), FilterFieldType.Number),
				() => VotesReason(condition, rating)
			),
			_ => new None(),
		};
	}

	private static string RatingReason(FilterCondition condition, Optional<SammlarrRating> rating, RatingStrategy strategy)
	{
		return rating.Match(found => $"Rating {RatingTexts.Number(found)} ({By(found, strategy)}) {Comparison(condition)}", () => Unrated);
	}

	private static string VotesReason(FilterCondition condition, Optional<SammlarrRating> rating)
	{
		return rating.Match(found => found.Votes.Match(votes => $"Votes {RatingTexts.Count(votes)} {Comparison(condition)}", () => Unvoted(found)), () => Unrated);
	}

	private static string By(SammlarrRating rating, RatingStrategy strategy)
	{
		return strategy switch
		{
			AveragedRating => "the average",
			OrderedRating => $"{RatingTexts.Name(rating.Counted[0].Source)} {rating.Counted[0].Value}, first in this feed's order",
		};
	}

	private static string Unvoted(SammlarrRating rating)
	{
		var silent = rating.Counted.Select(part => part.Source).Where(source => !source.Card.PublishesVotes).Distinct().Select(RatingTexts.Name).ToList();
		return silent.Count switch
		{
			0 => "no votes behind its rating (no vote count came with it)",
			1 => $"no votes behind its rating ({silent[0]} publishes none)",
			_ => $"no votes behind its rating ({string.Join(", ", silent)} publish none)",
		};
	}

	private static string Comparison(FilterCondition condition)
	{
		return condition.Operator switch
		{
			FilterOperator.GreaterOrEqual => $"is below {Shown(condition.Value)}",
			FilterOperator.GreaterThan => $"is not above {Shown(condition.Value)}",
			FilterOperator.LessOrEqual => $"is above {Shown(condition.Value)}",
			FilterOperator.LessThan => $"is not below {Shown(condition.Value)}",
			FilterOperator.Between => $"is outside {Shown(condition.Value)} to {Shown(condition.ValueEnd)}",
			FilterOperator.Equals => $"is not {Shown(condition.Value)}",
			_ => "does not match the rule",
		};
	}

	private static string Shown(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
