using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class TraktPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title) => ids.OfProvider(MediaProvider.Trakt).Map(Page);

	private static Optional<string> Page(IExternalMediaId id)
	{
		return id.Type switch
		{
			MediaType.Movie => Some.Of($"https://app.trakt.tv/movies/{id.Value}"),
			MediaType.Show => Some.Of($"https://app.trakt.tv/shows/{id.Value}"),
			MediaType.Unknown => new None(),
		};
	}
}
