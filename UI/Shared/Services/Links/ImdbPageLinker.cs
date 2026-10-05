using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class ImdbPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.Imdb).Map(id => $"https://www.imdb.com/title/{id.Value}");
	}
}
