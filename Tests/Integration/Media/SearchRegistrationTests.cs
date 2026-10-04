using Contracts.Media.Search;
using Domain.Providers;
using Harness;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SearchRegistrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	[Arguments(SearchProvider.Tmdb, "TmdbSearch")]
	[Arguments(SearchProvider.Arr, "ArrSearch")]
	[Arguments(SearchProvider.Plex, "PlexApi")]
	public async Task Search_ForEveryProvider_ResolvesItsOwnSearch(SearchProvider provider, string implementation)
	{
		var search = host.ResolveKeyed<IMediaSearchProvider>(provider);

		await Assert.That(search.GetType().Name).IsEqualTo(implementation);
	}
}
