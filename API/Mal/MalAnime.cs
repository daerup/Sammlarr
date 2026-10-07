using System.Globalization;
using API.Mal.Types;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.Mal;

internal static class MalAnime
{
	internal const string Fields =
		"id,title,alternative_titles,media_type,start_date,synopsis,main_picture,genres,num_episodes,average_episode_duration,mean,num_scoring_users";

	internal static MediaType TypeOf(MalTypes.Anime anime) => anime.MediaType == "movie" ? MediaType.Movie : MediaType.Show;

	internal static MalId Id(MalTypes.Anime anime, MediaType type) => MalId.From(anime.Id.ToString(CultureInfo.InvariantCulture), type);

	internal static Optional<IdentifyHints> Hints(MalTypes.Anime anime, MediaType type)
	{
		return Optional
			.Of(Title(anime))
			.Map(title => new IdentifyHints
			{
				Title = title,
				Year = ProviderValues.Date(anime.StartDate)?.Year ?? 0,
				MediaType = type,
			});
	}

	internal static MediaDetails Details(MalTypes.Anime anime, MediaType type)
	{
		var details = MediaDetails.Empty(type) with
		{
			Title = Title(anime),
			Summary = ProviderValues.Text(anime.Synopsis),
			PosterUrl = ProviderValues.Text(anime.MainPicture?.Large) ?? ProviderValues.Text(anime.MainPicture?.Medium),
			ReleaseDate = ProviderValues.Date(anime.StartDate),
			Genres = ProviderValues.Genres(anime.Genres?.Select(genre => genre.Name)),
			Ratings = Optional
				.Of(anime.Mean)
				.Map(mean => MediaRatings.From([new MyAnimeListRating(mean, Optional.Of(anime.NumScoringUsers).When(votes => votes > 0))]))
				.GetValueOr(MediaRatings.None),
		};
		var runtime = ProviderValues.Minutes(anime.AverageEpisodeDuration / 60);
		return details switch
		{
			MovieDetails movie => movie with { Runtime = runtime },
			ShowDetails show => show with { Episodes = ProviderValues.Count(anime.NumEpisodes), RuntimePerEpisode = runtime },
			UnknownDetails unknown => unknown,
		};
	}

	private static string? Title(MalTypes.Anime anime) => ProviderValues.Text(anime.AlternativeTitles?.En) ?? ProviderValues.Text(anime.Title);
}
