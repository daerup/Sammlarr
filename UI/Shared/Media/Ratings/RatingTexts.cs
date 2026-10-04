using System.Globalization;
using Domain.Media;
using Domain.Providers;
using UI.Shared.Services.Branding;

namespace UI.Shared.Media.Ratings;

internal static class RatingTexts
{
	public const string Footer = "Out of 10. Hover a value for the provider's own number, the rating above for the arithmetic.";

	public const string UnratedFooter = "No provider rates this title yet; it gets a Sammlarr rating as soon as one does.";

	public const string Unrated = "No Sammlarr rating yet";

	public const string LastOneOn = "At least one provider has to count, so the last one switched on stays on.";

	public const string StoredFooter = "Browse only has the ratings Sammlarr stores for this title; opening it loads the rest.";

	private const string RottenTomatoesSite = "https://www.rottentomatoes.com/";

	private const string RottenTomatoesSearch = "https://www.rottentomatoes.com/search?search=";

	public static string Name(RatingSource source) => source.GetDescription();

	public static string Short(RatingSource source)
	{
		return source switch
		{
			RatingSource.Tmdb => "TMDB",
			RatingSource.Imdb => "IMDb",
			RatingSource.RottenTomatoesCritics => "RT critics",
			RatingSource.RottenTomatoesAudience => "RT audience",
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

	public static string Detail(IMediaRating rating)
	{
		return rating.Source switch
		{
			RatingSource.Tmdb or RatingSource.Imdb => rating.Votes.Match(votes => $"{Count(votes)} votes", () => "no vote count"),
			RatingSource.RottenTomatoesCritics => $"{rating.Value} of critics liked it",
			RatingSource.RottenTomatoesAudience => $"{rating.Value} of users liked it",
		};
	}

	public static string Reason(RatingRowState state, Optional<SammlarrRating> sammlarr, string where)
	{
		return state switch
		{
			RatingRowState.NotNeeded => $"not needed, {sammlarr.Map(rating => Name(rating.From[0].Source)).GetValueOr(string.Empty)} came first",
			RatingRowState.SwitchedOff => $"switched off in {where}",
			RatingRowState.NoRating => "no rating for this title",
			RatingRowState.NeedsProvider => "needs a provider",
			RatingRowState.Counts => string.Empty,
		};
	}

	public static string Arithmetic(SammlarrRating rating, RatingStrategy strategy)
	{
		if (rating.From.Count > 1)
		{
			var parts = string.Join(" + ", rating.From.Select(part => Number(part.SammlarrValue)));
			return $"({parts}) / {rating.From.Count} = {rating.Value.ToString("0.00", CultureInfo.InvariantCulture)}";
		}

		var only = rating.From[0];
		return $"{Name(only.Source)} {only.Value} → {Number(rating)}, {Why(strategy)}";
	}

	public static string VoteParts(SammlarrRating rating) => string.Join(" + ", rating.From.Select(part => part.Votes).WhereSome().Select(Count));

	public static int VoteSources(SammlarrRating rating) => rating.From.Count(part => part.Votes.IsSome());

	public static Optional<string> Link(RatingSource source, IReadOnlyList<IExternalMediaId> ids, string? title, Optional<string> rottenTomatoesPath)
	{
		return source switch
		{
			RatingSource.Tmdb => Page(ids, MediaProvider.Tmdb),
			RatingSource.Imdb => Page(ids, MediaProvider.Imdb),
			RatingSource.RottenTomatoesCritics or RatingSource.RottenTomatoesAudience => rottenTomatoesPath
				.Map(path => $"{RottenTomatoesSite}{path}")
				.Or(() => Optional.Of(title).Map(name => $"{RottenTomatoesSearch}{Uri.EscapeDataString(name)}")),
		};
	}

	private static Optional<string> Page(IReadOnlyList<IExternalMediaId> ids, MediaProvider provider)
	{
		return ids.FirstOrNone(id => id.Source == provider).Map(id => Optional.Of(ExternalMediaIdUrlResolver.Resolve(id)));
	}

	private static string Why(RatingStrategy strategy)
	{
		return strategy switch
		{
			AveragedRating => "the only rating",
			OrderedRating => "first in the order",
		};
	}
}
