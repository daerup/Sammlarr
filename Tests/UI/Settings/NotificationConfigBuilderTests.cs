using UI.Features.Settings.Components.Notifications;

namespace UI.Tests.Settings;

public class NotificationConfigBuilderTests
{
	[Test]
	public async Task Webhook_WhenTheUrlIsPadded_TrimsIt()
	{
		var config = NotificationConfigBuilder.Webhook("  https://example.test/hook  ", "whsec_x");

		await Assert.That(config.Url).IsEqualTo("https://example.test/hook");
		await Assert.That(config.SigningSecret).IsEqualTo("whsec_x");
	}

	[Test]
	public async Task Pushover_WhenValuesArePadded_TrimsThem()
	{
		var config = NotificationConfigBuilder.Pushover("  new-token ", " new-key ");

		await Assert.That(config.AppToken).IsEqualTo("new-token");
		await Assert.That(config.UserKey).IsEqualTo("new-key");
	}
}
