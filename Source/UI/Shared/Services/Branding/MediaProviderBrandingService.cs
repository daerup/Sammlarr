using Contracts.Media;
using TimeWarp.Simple.Icons;

namespace UI.Shared.Services.Branding;

public class MediaProviderBrandingService(IWebHostEnvironment env)
{
	public BrandingInfo GetBrandingInfo(MediaProvider provider) =>
		provider switch
		{
			MediaProvider.Plex => BrandingInfoBuilder.SimpleIcon<PlexIcon>("Plex", "plex", "#E5A00D"),
			MediaProvider.Tmdb => BrandingInfoBuilder.SimpleIcon<ThemoviedatabaseIcon>("TMDb", "themoviedatabase", "#01B4E4"),
			MediaProvider.Imdb => BrandingInfoBuilder.Asset("IMDb", "imdb", "#F5DE50", "assets/icons/imdb.svg", env.WebRootPath),
			MediaProvider.Tvdb => BrandingInfoBuilder.Asset("TheTVDB", "tvdb", "#23A962", "assets/icons/tvdb.svg", env.WebRootPath),
		};
}
