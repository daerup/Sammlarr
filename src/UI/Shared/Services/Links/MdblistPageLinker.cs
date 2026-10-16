using Domain.Media;
using Domain.Providers;

namespace UI.Shared.Services.Links;

internal sealed class MdblistPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title) => ids.OfProvider(MediaProvider.Mdblist).Map(Page);

	private static Optional<string> Page(IExternalMediaId id)
	{
		return id.Type switch
		{
			MediaType.Movie => Some.Of($"https://mdblist.com/movie/{id.Value}"),
			MediaType.Show => Some.Of($"https://mdblist.com/show/{id.Value}"),
			MediaType.Unknown => new None(),
		};
	}
}
