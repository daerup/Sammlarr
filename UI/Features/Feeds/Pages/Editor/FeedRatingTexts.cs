using Domain.Media;
using UI.Shared.Media.Ratings;

namespace UI.Features.Feeds.Pages.Editor;

internal static class FeedRatingTexts
{
	public static string StarTip(Optional<RatingStrategy> own, RatingStrategy fallback)
	{
		var setting = own.Match(strategy => $"{RatingTexts.Strategy(strategy)} · {Listed(strategy)}", () => $"My default ({RatingTexts.Strategy(fallback)})");
		return $"This feed's Sammlarr rating: {setting}. Click to change.";
	}

	public static string VotesTip(Optional<RatingStrategy> own, RatingStrategy fallback)
	{
		var strategy = own.GetValueOr(fallback);
		var whose = own.Match(_ => RatingTexts.Strategy(strategy), () => $"your default, {RatingTexts.Strategy(strategy)}");
		return strategy switch
		{
			AveragedRating =>
				$"Votes counts the people behind this feed's rating ({whose}): the votes of every provider it averages ({Listed(strategy)}), added up. Rotten Tomatoes publishes no votes, so it adds none.",
			OrderedRating =>
				$"Votes counts the people behind this feed's rating ({whose}): the votes of the one provider that gave it, in this order: {Listed(strategy)}. Rotten Tomatoes publishes no votes, so a title it rates fails a Votes rule.",
		};
	}

	private static string Listed(RatingStrategy strategy) => string.Join(", ", Counted(strategy).Select(RatingTexts.Short));

	private static IEnumerable<RatingSource> Counted(RatingStrategy strategy)
	{
		return strategy switch
		{
			AveragedRating averaged => averaged.SwitchedOn.Order(),
			OrderedRating ordered => ordered.Order,
		};
	}
}
