using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class MetacriticPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.Metacritic).Map(id => $"https://www.metacritic.com/{(id.Type == MediaType.Show ? "tv" : "movie")}/{id.Value}");
	}
}
