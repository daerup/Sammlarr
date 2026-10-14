using Core.DI;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.DI;

public class KeyedServiceSetTests
{
	private static readonly Named Plex = new("plex");
	private static readonly Named Tmdb = new("tmdb");

	[Test]
	public async Task Keys_ListsTheRegisteredKeysInTheirDeclaredOrder()
	{
		var testee = new KeyedServiceSet<RemoteService, Named>([new(RemoteService.Plex, Plex), new(RemoteService.Tmdb, Tmdb)]);

		await Assert.That(testee.Keys).IsEquivalentTo([RemoteService.Tmdb, RemoteService.Plex], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Find_ForARegisteredKey_IsItsService()
	{
		var testee = new KeyedServiceSet<RemoteService, Named>([new(RemoteService.Tmdb, Tmdb)]);

		await Assert.That(testee.Find(RemoteService.Tmdb)).IsSome().And.IsSameReferenceAs(Tmdb);
	}

	[Test]
	public async Task Find_ForAKeyNothingServes_IsNone()
	{
		var testee = new KeyedServiceSet<RemoteService, Named>([new(RemoteService.Tmdb, Tmdb)]);

		await Assert.That(testee.Find(RemoteService.Wikidata)).IsNone();
	}

	private sealed record Named(string Name);
}
