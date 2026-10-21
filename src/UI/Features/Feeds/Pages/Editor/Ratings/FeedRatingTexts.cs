using Domain.Media;
using UI.Shared.Media.Ratings;

namespace UI.Features.Feeds.Pages.Editor.Ratings;

internal static class FeedRatingTexts
{
	public static string StarTip(Optional<RatingStrategy> own, RatingStrategy fallback)
	{
		var setting = own.Match(strategy => $"{RatingTexts.Strategy(strategy)} · {Listed(strategy)}", () => $"My default ({RatingTexts.Strategy(fallback)})");
		return $"This feed's Sammlarr rating: {setting}. Click to change.";
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
