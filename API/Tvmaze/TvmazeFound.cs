using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using API.Tvmaze.Types;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.Tvmaze;

internal static partial class TvmazeFound
{
	internal static IReadOnlyList<IExternalMediaId> Ids(TvmazeTypes.Show show)
	{
		Optional<IExternalMediaId>[] found =
		[
			Some.Of(Id(TvmazeId.From(Number(show.Id), MediaType.Show))),
			Optional.Of(show.Externals?.Thetvdb).Map(tvdb => Id(TvdbId.From(Number(tvdb), MediaType.Show))),
			Optional.Of(ProviderValues.Text(show.Externals?.Imdb)).Map(imdb => Id(ImdbId.From(imdb, MediaType.Show))),
		];
		return [.. found.WhereSome()];
	}

	internal static Optional<IdentifyHints> Hints(TvmazeTypes.Show show)
	{
		return Optional
			.Of(ProviderValues.Text(show.Name))
			.Map(name => new IdentifyHints
			{
				Title = name,
				Year = ProviderValues.Date(show.Premiered)?.Year ?? 0,
				MediaType = MediaType.Show,
			});
	}

	internal static ShowDetails Details(TvmazeTypes.Show show)
	{
		return new ShowDetails
		{
			Title = ProviderValues.Text(show.Name),
			Summary = ProviderValues.Text(Plain(show.Summary)),
			PosterUrl = ProviderValues.Text(show.Image?.Original),
			ReleaseDate = ProviderValues.Date(show.Premiered),
			Genres = ProviderValues.Genres(show.Genres),
			RuntimePerEpisode = ProviderValues.Minutes(show.Runtime ?? show.AverageRuntime),
		};
	}

	private static string? Plain(string? html) => html is null ? null : WebUtility.HtmlDecode(Tags.Replace(html, string.Empty)).Trim();

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static IExternalMediaId Id(IExternalMediaId id) => id;

	[GeneratedRegex("<[^>]*>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex Tags { get; }
}
