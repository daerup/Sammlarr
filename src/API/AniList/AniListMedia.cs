using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using API.AniList.Types;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.AniList;

internal static partial class AniListMedia
{
	internal const string Fields =
		"id idMal format title { english romaji } description(asHtml: false) coverImage { extraLarge } startDate { year month day } genres episodes duration averageScore stats { scoreDistribution { score amount } }";

	internal static bool IsTitle(AniListTypes.Media media) => media.Format is not "MUSIC";

	internal static MediaType TypeOf(AniListTypes.Media media) => media.Format == "MOVIE" ? MediaType.Movie : MediaType.Show;

	internal static IReadOnlyList<IExternalMediaId> Ids(AniListTypes.Media media, MediaType type)
	{
		Optional<IExternalMediaId>[] found =
		[
			Some.Of(Id(AniListId.From(Number(media.Id), type))),
			Optional.Of(media.IdMal).Map(mal => Id(MalId.From(Number(mal), type))),
		];
		return [.. found.WhereSome()];
	}

	internal static Optional<IdentifyHints> Hints(AniListTypes.Media media, MediaType type)
	{
		return Optional
			.Of(Title(media))
			.Map(title => new IdentifyHints
			{
				Title = title,
				Year = media.StartDate?.Year ?? 0,
				MediaType = type,
			});
	}

	private static string? Title(AniListTypes.Media media) => ProviderValues.Text(media.Title?.English) ?? ProviderValues.Text(media.Title?.Romaji);

	internal static MediaDetails Details(AniListTypes.Media media, MediaType type)
	{
		var details = MediaDetails.Empty(type) with
		{
			Title = Title(media),
			Summary = ProviderValues.Text(Plain(media.Description)),
			PosterUrl = ProviderValues.Text(media.CoverImage?.ExtraLarge),
			ReleaseDate = Released(media.StartDate),
			Genres = ProviderValues.Genres(media.Genres),
			Ratings = Optional.Of(media.AverageScore).Map(score => MediaRatings.From([new AniListRating(score, Votes(media))])).GetValueOr(MediaRatings.None),
		};
		return details switch
		{
			MovieDetails movie => movie with { Runtime = ProviderValues.Minutes(media.Duration) },
			ShowDetails show => show with { Episodes = ProviderValues.Count(media.Episodes), RuntimePerEpisode = ProviderValues.Minutes(media.Duration) },
			UnknownDetails unknown => unknown,
		};
	}

	private static Optional<int> Votes(AniListTypes.Media media)
	{
		return Optional.Of(media.Stats?.ScoreDistribution).Map(scores => scores.Sum(score => score.Amount)).When(votes => votes > 0);
	}

	private static DateOnly? Released(AniListTypes.Date? date)
	{
		return date is { Year: { } year, Month: { } month, Day: { } day } ? new DateOnly(year, month, day) : ProviderValues.YearStart(date?.Year);
	}

	private static string? Plain(string? html) => html is null ? null : WebUtility.HtmlDecode(Tags.Replace(html, string.Empty)).Trim();

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static IExternalMediaId Id(IExternalMediaId id) => id;

	[GeneratedRegex("<[^>]*>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex Tags { get; }
}
