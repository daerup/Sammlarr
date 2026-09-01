using Domain.Notifications;
using UI.Features.Settings.Components;

namespace UI.Tests.Settings;

public class NotificationConfigBuilderTests
{
	[Test]
	public async Task Webhook_trims_the_url()
	{
		var config = NotificationConfigBuilder.Webhook("  https://example.test/hook  ", "whsec_x");

		await Assert.That(config.Url).IsEqualTo("https://example.test/hook");
		await Assert.That(config.SigningSecret).IsEqualTo("whsec_x");
	}

	[Test]
	public async Task Pushover_trims_the_entered_values()
	{
		var config = NotificationConfigBuilder.Pushover("  new-token ", " new-key ");

		await Assert.That(config.AppToken).IsEqualTo("new-token");
		await Assert.That(config.UserKey).IsEqualTo("new-key");
	}
}
