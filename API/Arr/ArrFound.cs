using API.Arr.Types;
using Domain.Media;
using Domain.Wantlist.Arr;

namespace API.Arr;

internal static class ArrFound
{
	internal static FoundMedia Of(ArrApp app, ArrTitle title)
	{
		Optional<IExternalMediaId>[] ids =
		[
			Optional.When(app is ArrApp.Radarr, () => (IExternalMediaId)TmdbId.From(title.TmdbId.ToString(), MediaType.Movie)),
			Optional.When(app is ArrApp.Sonarr, () => (IExternalMediaId)TvdbId.From(title.TvdbId.ToString(), MediaType.Show)),
			Optional.When(app is ArrApp.Sonarr && title.TmdbId > 0, () => (IExternalMediaId)TmdbId.From(title.TmdbId.ToString(), MediaType.Show)),
			Optional.Of(title.ImdbId).When(imdb => imdb.Length > 0).Map(imdb => (IExternalMediaId)ImdbId.From(imdb, app.Type)),
		];
		return new FoundMedia(
			[.. ids.WhereSome()],
			MediaDetails.Empty(app.Type) with
			{
				Title = title.Title,
				Summary = ProviderValues.Text(title.Overview),
				PosterUrl = ProviderValues.Text(title.RemotePoster),
				ReleaseDate = ProviderValues.YearStart(title.Year),
			}
		);
	}
}
