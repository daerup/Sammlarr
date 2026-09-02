using UI.Features.Settings.Components.Notifications;

namespace UI.Tests.Settings;

public class WebhookSecretTests
{
	[Test]
	public async Task Generate_is_whsec_prefixed_and_decodes_to_32_bytes()
	{
		var secret = WebhookSecret.Generate();

		await Assert.That(secret).StartsWith("whsec_");
		await Assert.That(Convert.FromBase64String(secret["whsec_".Length..]).Length).IsEqualTo(32);
	}

	[Test]
	public async Task Generate_produces_a_fresh_secret_each_call()
	{
		await Assert.That(WebhookSecret.Generate()).IsNotEqualTo(WebhookSecret.Generate());
	}
}
