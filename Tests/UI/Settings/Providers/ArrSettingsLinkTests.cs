using TestInfrastructure;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ArrSettingsLinkTests
{
	[Test]
	[Arguments("http://radarr:7878", "http://radarr:7878/settings/general")]
	[Arguments("http://host/radarr/", "http://host/radarr/settings/general")]
	[Arguments("https://radarr.example.com", "https://radarr.example.com/settings/general")]
	[Arguments("http://radarr:7878/?page=1#top", "http://radarr:7878/settings/general")]
	public async Task For_AnAbsoluteHttpUrl_LinksToItsGeneralSettings(string url, string link)
	{
		await Assert.That(ArrSettingsLink.For(url)).IsSome().And.IsEqualTo(link);
	}

	[Test]
	[Arguments("radarr:7878")]
	[Arguments("")]
	[Arguments("ftp://radarr")]
	public async Task For_AnythingElse_GivesNone(string url)
	{
		await Assert.That(ArrSettingsLink.For(url)).IsNone();
	}
}
