using Domain.Providers;
using TestInfrastructure;

namespace Domain.Tests.Providers;

public class AuthProviderConversionsTests
{
	[Test]
	public async Task Provider_ForPlex_IsPlex()
	{
		await Assert.That(AuthProvider.Plex.Provider()).IsSome().And.IsEqualTo(MediaProvider.Plex);
	}

	[Test]
	public async Task Provider_ForSammlarrAccounts_IsNone()
	{
		await Assert.That(AuthProvider.Local.Provider()).IsNone();
	}
}
