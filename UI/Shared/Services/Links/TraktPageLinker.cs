using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class TraktPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.Trakt).Map(id => $"https://trakt.tv/search/trakt/{id.Value}?id_type={(id.Type == MediaType.Show ? "show" : "movie")}");
	}
}
