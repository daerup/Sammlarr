using System.Globalization;
using Domain.Media;

namespace UI.Shared.Media.Ratings;

internal static class RatingTexts
{
	public const string Footer = "Out of 10. Hover a value for the provider's own number, the rating above for the arithmetic.";

	public const string UnratedFooter = "No provider rates this title yet; it gets a Sammlarr rating as soon as one does.";

	public const string Unrated = "No Sammlarr rating yet";

	public const string LastOneOn = "At least one provider has to count, so the last one switched on stays on.";

	public const string StoredFooter = "Browse only has the ratings Sammlarr stores for this title; opening it loads the rest.";

	public static string Name(RatingSource source) => source.GetDescription();

	public static string Short(RatingSource source)
	{
		return source switch
		{
			RatingSource.Tmdb => "TMDB",
			RatingSource.Imdb => "IMDb",
			RatingSource.RottenTomatoesCritics => "RT critics",
			RatingSource.RottenTomatoesAudience => "RT audience",
			RatingSource.Metacritic => "Metacritic",
			RatingSource.Letterboxd => "Letterboxd",
			RatingSource.Trakt => "Trakt",
			RatingSource.MyAnimeList => "MAL",
			RatingSource.AniList => "AniList",
		};
	}

	public static string Scale(RatingSource source)
	{
		return source switch
		{
			RatingSource.Tmdb => "TMDB: its users rate from 0.5 to 10 in half steps, and the average counts as it reads (8.6 is 8.6).",
			RatingSource.Imdb => "IMDb: its users rate from 1 to 10, never 0, so an IMDb rating is at least 1.0. It counts as it reads (8.2 is 8.2).",
			RatingSource.RottenTomatoesCritics => "Rotten Tomatoes critics: the share of critics who liked it, 0 to 100%, counted as a tenth (95% is 9.5).",
			RatingSource.RottenTomatoesAudience => "Rotten Tomatoes audience: the share of users who liked it, 0 to 100%, counted as a tenth (95% is 9.5).",
			RatingSource.Metacritic => "Metacritic: the Metascore, a weighted average of critics' reviews from 0 to 100, counted as a tenth (73 is 7.3).",
			RatingSource.Letterboxd => "Letterboxd: its users rate from half a star to five stars, counted twice (3.9 stars is 7.8).",
			RatingSource.Trakt => "Trakt: its users' average as a share, 0 to 100%, counted as a tenth (84% is 8.4).",
			RatingSource.MyAnimeList => "MyAnimeList: its users rate from 1 to 10, and the average counts as it reads (8.62 is 8.6).",
			RatingSource.AniList => "AniList: the average of its users' scores out of 100, counted as a tenth (85% is 8.5).",
		};
	}

	public static string Number(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

	public static string Number(SammlarrRating rating) => rating.Rounded.ToString("0.0", CultureInfo.InvariantCulture);

	public static string Count(int votes) => votes.ToString("N0", CultureInfo.InvariantCulture);

	public static string Conversion(IMediaRating rating) => $"{rating.Value} → {Number(rating.SammlarrValue)}";

	public static string Strategy(RatingStrategy strategy)
	{
		return strategy switch
		{
			AveragedRating => "Average",
			OrderedRating => "First available",
		};
	}

	public static string Detail(IMediaRating rating, bool series)
	{
		return rating.Source switch
		{
			RatingSource.MyAnimeList or RatingSource.AniList when series => $"season 1 · {Votes(rating)}",
			RatingSource.Tmdb or RatingSource.Imdb or RatingSource.Letterboxd or RatingSource.Trakt or RatingSource.MyAnimeList or RatingSource.AniList => Votes(
				rating
			),
			RatingSource.Metacritic => $"a Metascore of {rating.Value}",
			RatingSource.RottenTomatoesCritics => $"{rating.Value} of critics liked it",
			RatingSource.RottenTomatoesAudience => $"{rating.Value} of users liked it",
		};
	}

	private static string Votes(IMediaRating rating) => rating.Votes.Match(votes => $"{Count(votes)} votes", () => "no vote count");

	public static string Reason(RatingRowState state, Optional<SammlarrRating> sammlarr, string where)
	{
		return state switch
		{
			RatingRowState.NotNeeded => $"not needed, {sammlarr.Map(rating => Name(rating.Counted[0].Source)).GetValueOr(string.Empty)} came first",
			RatingRowState.SwitchedOff => $"switched off in {where}",
			RatingRowState.Skipped => "no rating, skipped",
			RatingRowState.NoRating or RatingRowState.Counts => string.Empty,
		};
	}

	public static IReadOnlyList<RatingTerm> Terms(SammlarrRating rating)
	{
		return [.. InRowOrder(rating).Select(part => new RatingTerm(part, Number(part.SammlarrValue)))];
	}

	public static IReadOnlyList<RatingTerm> VoteTerms(SammlarrRating rating)
	{
		return [.. InRowOrder(rating).Select(part => part.Votes.Map(votes => new RatingTerm(part, Count(votes)))).WhereSome()];
	}

	public static int VoteSources(SammlarrRating rating) => rating.Counted.Count(part => part.Votes.IsSome());

	public static string Why(RatingStrategy strategy)
	{
		return strategy switch
		{
			AveragedRating => "the only rating",
			OrderedRating => "first in the order",
		};
	}

	private static IEnumerable<IMediaRating> InRowOrder(SammlarrRating rating) => rating.Counted.OrderBy(part => part.Source);
}

public sealed record RatingTerm(IMediaRating Rating, string Number);
