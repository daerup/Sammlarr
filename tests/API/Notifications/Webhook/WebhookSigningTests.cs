using API.Notifications.Webhook;

namespace API.Tests.Notifications.Webhook;

public class WebhookSigningTests
{
	[Test]
	public async Task GenerateSecret_WhenCalled_IsWhsecPrefixedAndDecodesTo32Bytes()
	{
		var secret = WebhookSigning.GenerateSecret();

		await Assert.That(secret).StartsWith("whsec_");
		await Assert.That(Convert.FromBase64String(secret["whsec_".Length..]).Length).IsEqualTo(32);
	}
}
