using API.Plex;
using Contracts.DI;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class PlexRegistrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task LookUpAsync_ForThePlexIdsOfATitleThenItsDetailsInOneScope_AsksPlexForItsMetadataOnce()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);
		var plexId = PlexId.From("550", MediaType.Movie);
		var converter = host.Resolve<IEnumerable<Keyed<RemoteService, IExternalIdConverter>>>().Single(keyed => keyed.Key == RemoteService.Plex).Capability;

		var ids = await converter.LookUpAsync(new ByIdRequest(ByRatingKey(converter), plexId));
		var details = await host.Enricher(RemoteService.Plex).LookUpAsync(plexId, []);

		await Assert.That(ids).IsFound();
		await Assert.That(details).IsFound();
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MetadataPath)).IsEqualTo(1);
	}

	private static ById ByRatingKey(IExternalIdConverter converter)
	{
		return converter
			.Lookups.SelectMany<IdLookup, ById>(each =>
				each switch
				{
					ById byId => [byId],
					ByHints => [],
				}
			)
			.Single(byId => byId.Accepts.Contains(MediaProvider.Plex));
	}
}
