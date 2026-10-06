using Domain.Providers;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Providers;

public class ServiceTests
{
	[Test]
	public async Task Remotes_OfTheRadarrAndSonarrPair_AreBothApps()
	{
		Service pair = new Arr();

		await Assert.That(pair.Remotes).IsEquivalentTo([RemoteService.Radarr, RemoteService.Sonarr], CollectionOrdering.Any);
	}

	[Test]
	public async Task Remotes_OfSammlarrAccounts_AreNone()
	{
		Service accounts = new SammlarrAccounts();

		await Assert.That(accounts.Remotes).IsEmpty();
	}

	[Test]
	public async Task DependsOn_ARemoteService_IsTrueOnlyForItself()
	{
		Service tmdb = RemoteService.Tmdb;

		await Assert.That(tmdb.DependsOn(RemoteService.Tmdb)).IsTrue();
		await Assert.That(tmdb.DependsOn(RemoteService.Plex)).IsFalse();
	}

	[Test]
	public async Task Equality_ComparesTheCaseByValue()
	{
		Service first = new Arr();
		Service second = new Arr();
		Service plex = RemoteService.Plex;

		await Assert.That(first == second).IsTrue();
		await Assert.That(plex == RemoteService.Plex).IsTrue();
		await Assert.That(plex == first).IsFalse();
		await Assert.That(new HashSet<Service> { first, second, plex }).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Service_OfSammlarrSignIn_IsSammlarrAccounts()
	{
		await Assert.That(AuthProvider.Local.Service == new SammlarrAccounts()).IsTrue();
		await Assert.That(AuthProvider.Plex.Service == RemoteService.Plex).IsTrue();
	}

	[Test]
	public async Task Service_OfTheArrChoices_IsThePair()
	{
		await Assert.That(SinkKind.Arr.Service == new Arr()).IsTrue();
		await Assert.That(SearchProvider.Arr.Service == new Arr()).IsTrue();
	}
}
