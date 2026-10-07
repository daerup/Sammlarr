using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class AniListPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.AniList).Map(id => $"https://anilist.co/anime/{id.Value}");
	}
}
