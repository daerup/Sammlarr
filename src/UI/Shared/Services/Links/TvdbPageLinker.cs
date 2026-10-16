using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class TvdbPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title) => ids.OfProvider(MediaProvider.Tvdb).Map(Page);

	private static Optional<string> Page(IExternalMediaId id)
	{
		return id.Type switch
		{
			MediaType.Movie => Some.Of($"https://thetvdb.com/dereferrer/movie/{id.Value}"),
			MediaType.Show => Some.Of($"https://thetvdb.com/dereferrer/series/{id.Value}"),
			MediaType.Unknown => new None(),
		};
	}
}
