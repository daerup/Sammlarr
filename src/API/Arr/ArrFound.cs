using API.Arr.Types;
using Domain.Media;
using Domain.Wantlist.Arr;

namespace API.Arr;

internal static class ArrFound
{
	internal static FoundMedia Of(ArrApi api, ArrTitle title)
	{
		Optional<IExternalMediaId>[] imdb = [Optional.Of(title.ImdbId).When(id => id.Length > 0).Map(id => (IExternalMediaId)ImdbId.From(id, api.App.Type))];
		return new FoundMedia(
			[.. api.IdsOf(title), .. imdb.WhereSome()],
			MediaDetails.Empty(api.App.Type) with
			{
				Title = title.Title,
				Summary = ProviderValues.Text(title.Overview),
				PosterUrl = ProviderValues.Text(title.RemotePoster),
				ReleaseDate = ProviderValues.YearStart(title.Year),
			}
		);
	}
}
