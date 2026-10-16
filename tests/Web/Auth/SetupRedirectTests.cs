using Domain.Providers;
using Web.Auth;

namespace Web.Tests.Auth;

public class SetupRedirectTests
{
	[Test]
	[Arguments(AuthProvider.Local, "/setup?error=That%20is%20not%20the%20setup%20code.&with=local")]
	[Arguments(AuthProvider.Plex, "/setup?error=That%20is%20not%20the%20setup%20code.&with=plex")]
	public async Task For_AFailedSetup_ComesBackWithTheErrorAndTheWayThatWasPicked(AuthProvider signInWith, string redirect)
	{
		await Assert.That(SetupRedirect.For(Error.From("That is not the setup code."), signInWith)).IsEqualTo(redirect);
	}
}
