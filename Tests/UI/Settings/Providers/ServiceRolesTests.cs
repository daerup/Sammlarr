using Domain.Providers;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ServiceRolesTests
{
	public static IEnumerable<Func<(Service Service, string[] Roles)>> Services()
	{
		return
		[
			() => (RemoteService.Plex, ["Sign-in", "Wantlist", "Search"]),
			() => (RemoteService.Tmdb, ["Search", "Trending", "Feeds"]),
			() => (RemoteService.Mdblist, ["Feeds"]),
			() => (new Arr(), ["Wantlist", "Search"]),
			() => (new SammlarrAccounts(), ["Sign-in"]),
		];
	}

	[Test]
	[MethodDataSource(nameof(Services))]
	public async Task For_AService_NamesTheMapsRolesItCanFillInTheMapsOrder(Service service, string[] roles)
	{
		await Assert.That(ServiceRoles.For(service).Select(role => role.Name)).IsEquivalentTo(roles, CollectionOrdering.Matching);
	}
}
