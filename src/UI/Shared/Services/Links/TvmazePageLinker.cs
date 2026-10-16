using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class TvmazePageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.Tvmaze).Map(id => $"https://www.tvmaze.com/shows/{id.Value}");
	}
}
