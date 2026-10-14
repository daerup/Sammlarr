using System.Globalization;
using API.Trakt.Types;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;

namespace API.Trakt;

internal static class TraktTitle
{
	internal static string Kind(MediaType type) => type is MediaType.Show ? "shows" : "movies";

	internal static IdentifyRequest Request(TraktTypes.Title title, MediaType type) =>
		new([.. Ids(title, type)], Hints(title, type)) { Sources = [RemoteService.Trakt] };

	internal static IReadOnlyList<IExternalMediaId> Ids(TraktTypes.Title title, MediaType type)
	{
		Optional<IExternalMediaId>[] found =
		[
			Optional.Of(title.Ids?.Trakt).Map(value => Id(TraktId.From(Number(value), type))),
			Optional.Of(ProviderValues.Text(title.Ids?.Imdb)).Map(value => Id(ImdbId.From(value, type))),
			Optional.Of(title.Ids?.Tmdb).Map(value => Id(TmdbId.From(Number(value), type))),
			Optional.Of(title.Ids?.Tvdb).When(_ => type is MediaType.Show).Map(value => Id(TvdbId.From(Number(value), type))),
		];
		return [.. found.WhereSome()];
	}

	internal static Optional<IdentifyHints> Hints(TraktTypes.Title title, MediaType type)
	{
		return Optional
			.Of(ProviderValues.Text(title.Name))
			.Map(name => new IdentifyHints
			{
				Title = name,
				Year = title.Year ?? 0,
				MediaType = type,
			});
	}

	internal static MediaDetails Details(TraktTypes.Title title, MediaType type)
	{
		var details = MediaDetails.Empty(type) with
		{
			Summary = ProviderValues.Text(title.Overview),
			ReleaseDate = ProviderValues.Date(title.Released) ?? ProviderValues.Date(title.FirstAired?.Split('T')[0]),
			Genres = ProviderValues.Genres(title.Genres?.Select(Genre)),
			Ratings = Optional
				.Of(title.Rating)
				.When(rating => rating > 0)
				.Map(rating =>
					MediaRatings.From([new TraktRating((int)Math.Round(rating * 10, MidpointRounding.ToEven), Optional.Of(title.Votes).When(votes => votes > 0))])
				)
				.GetValueOr(MediaRatings.None),
		};
		var runtime = ProviderValues.Minutes(title.Runtime);
		return details switch
		{
			MovieDetails movie => movie with { Runtime = runtime },
			ShowDetails show => show with { RuntimePerEpisode = runtime },
			UnknownDetails unknown => unknown,
		};
	}

	private static string Genre(string slug) =>
		string.Join(' ', slug.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static IExternalMediaId Id(IExternalMediaId id) => id;
}
