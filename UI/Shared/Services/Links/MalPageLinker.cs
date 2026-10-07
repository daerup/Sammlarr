using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class MalPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.Mal).Map(id => $"https://myanimelist.net/anime/{id.Value}");
	}
}
