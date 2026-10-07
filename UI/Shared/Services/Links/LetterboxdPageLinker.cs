using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class LetterboxdPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.OfProvider(MediaProvider.Letterboxd).Map(id => $"https://letterboxd.com/film/{id.Value}/");
	}
}
