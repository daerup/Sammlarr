using DB.Data.Config;
using Domain.Providers;
using Domain.Settings;

namespace DB.Tests.Config;

public class ProviderConfigDefaultsTests
{
	[Test]
	public async Task Default_ForANewInstall_SearchesOnTmdbWithSignUpsOpenUntilTheFirstAdmin()
	{
		var seeded = ProviderConfigRow.Default;

		await Assert.That(seeded.SearchProvider).IsEqualTo(SearchProvider.Tmdb);
		await Assert.That(seeded.Sink).IsEqualTo(SinkKind.Plex);
		await Assert.That(seeded.RegistrationMode).IsEqualTo(RegistrationMode.Open);
		await Assert.That(seeded.OnboardingCompletedAt).IsNull();
	}
}
