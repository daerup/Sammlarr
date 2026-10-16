using API.Plex;
using Shared.Options;

namespace API.Tests.Plex;

internal static class PlexTestUrls
{
	internal static PlexUrls Instance { get; } =
		new(
			new PlexOptions
			{
				BaseUrl = "https://plex.tv",
				Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
			}
		);
}
