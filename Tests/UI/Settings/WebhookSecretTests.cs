using UI.Features.Settings.Components.Notifications;

namespace UI.Tests.Settings;

public class WebhookSecretTests
{
	[Test]
	public async Task Generate_WhenCalled_IsWhsecPrefixedAndDecodesTo32Bytes()
	{
		var secret = WebhookSecret.Generate();

		await Assert.That(secret).StartsWith("whsec_");
		await Assert.That(Convert.FromBase64String(secret["whsec_".Length..]).Length).IsEqualTo(32);
	}

	[Test]
	public async Task Generate_WhenCalledRepeatedly_ProducesAFreshSecret()
	{
		await Assert.That(WebhookSecret.Generate()).IsNotEqualTo(WebhookSecret.Generate());
	}
}
