using Contracts.DI;
using Core.DI;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
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

	[Test]
	public async Task KeyedServiceSet_ResolvedFromCoreModule_GathersEveryKeyedRegistration()
	{
		var services = new ServiceCollection();
		services.AddModule<CoreModule>();
		services.AddSingleton(new Keyed<RemoteService, Named>(RemoteService.Plex, Plex));
		await using var serviceProvider = services.BuildServiceProvider();
		using var scope = serviceProvider.CreateScope();

		var testee = scope.ServiceProvider.GetRequiredService<IKeyedServiceSet<RemoteService, Named>>();

		await Assert.That(testee.Keys).IsEquivalentTo([RemoteService.Plex]);
	}

	private sealed record Named(string Name);
}
