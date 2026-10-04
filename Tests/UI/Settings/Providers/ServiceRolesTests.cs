using Domain.Providers;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ServiceRolesTests
{
	[Test]
	[Arguments(MediaProvider.Plex, new[] { "Sign-in", "Wantlist", "Search" })]
	[Arguments(MediaProvider.Tmdb, new[] { "Search", "Trending", "Feeds" })]
	[Arguments(MediaProvider.Mdblist, new[] { "Feeds" })]
	[Arguments(MediaProvider.Local, new[] { "Sign-in" })]
	public async Task For_AProvider_NamesTheMapsRolesItCanFillInTheMapsOrder(MediaProvider provider, string[] roles)
	{
		await Assert.That(ServiceRoles.For(provider).Select(role => role.Name)).IsEquivalentTo(roles, CollectionOrdering.Matching);
	}
}
