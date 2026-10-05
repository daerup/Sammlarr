using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class RottenTomatoesPageLinker : IExternalPageLinker
{
	private const string Site = "https://www.rottentomatoes.com/";

	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.RottenTomatoes)
			.Map(id => $"{Site}{id.Value}")
			.Or(() => title.Map(name => $"{Site}search?search={Uri.EscapeDataString(name)}"));
	}
}
