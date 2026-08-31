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
	public async Task Pushover_keeps_existing_secrets_when_left_blank()
	{
		var existing = new PushoverConfig("old-token", "old-key");

		var config = NotificationConfigBuilder.Pushover("   ", "", existing);

		await Assert.That(config.AppToken).IsEqualTo("old-token");
		await Assert.That(config.UserKey).IsEqualTo("old-key");
	}

	[Test]
	public async Task Pushover_replaces_secrets_when_entered()
	{
		var existing = new PushoverConfig("old-token", "old-key");

		var config = NotificationConfigBuilder.Pushover("  new-token ", "new-key", existing);

		await Assert.That(config.AppToken).IsEqualTo("new-token");
		await Assert.That(config.UserKey).IsEqualTo("new-key");
	}

	[Test]
	public async Task Pushover_on_create_uses_the_entered_values()
	{
		var config = NotificationConfigBuilder.Pushover("token", "key", existing: null);

		await Assert.That(config.AppToken).IsEqualTo("token");
		await Assert.That(config.UserKey).IsEqualTo("key");
	}
}
