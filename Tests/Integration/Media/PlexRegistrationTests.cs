using Contracts.Media.Search;
using Domain.Providers;
using Harness;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class PlexRegistrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task SearchAndEnrichment_WithinOneScope_ShareOnePlexApi()
	{
		object search = host.ResolveKeyed<IMediaSearchProvider>(SearchProvider.Plex);

		await Assert.That(host.Enricher(RemoteService.Plex)).IsSameReferenceAs(search);
	}
}
