using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Shared;
using TimeWarp.Simple.Icons;

namespace UI.Shared.Services.Branding;

public class MediaProviderBrandingService(string webRoot)
{
	public BrandingInfo GetBrandingInfo(MediaProvider provider) =>
		provider switch
		{
			MediaProvider.Plex => BrandingInfoBuilder.SimpleIcon<PlexIcon>("Plex", "Plex", "#E5A00D"),
			MediaProvider.Tmdb => BrandingInfoBuilder.SimpleIcon<ThemoviedatabaseIcon>("The Movie Database", "TMDb", "#01B4E4"),
			MediaProvider.Imdb => BrandingInfoBuilder.Asset("IMDb", "IMDb", "#F5DE50", "assets/icons/imdb.svg", webRoot),
			MediaProvider.Tvdb => BrandingInfoBuilder.Asset("The TVDB", "TVDB", "#23A962", "assets/icons/tvdb.svg", webRoot),
			MediaProvider.Mdblist => BrandingInfoBuilder.SimpleIcon<MdblistIcon>("Movie Database List", "MDBList", "#4284CA"),
			_ => BrandingInfoBuilder.Lucide(provider.GetDescription(), provider.ToString(), "shield-question-mark", "#000000"),
		};
}
