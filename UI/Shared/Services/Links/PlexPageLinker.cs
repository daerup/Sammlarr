using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class PlexPageLinker : IExternalPageLinker
{
	private const string Discover = "https://app.plex.tv/desktop/#!/provider/tv.plex.provider.discover/details?key=%2Flibrary%2Fmetadata%2F";

	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.Plex).Map(id => $"{Discover}{id.Value}");
	}
}
