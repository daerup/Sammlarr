using System.Security.Cryptography;
using System.Text;
using API.Notifications.Webhook;

namespace API.Tests.Notifications.Webhook;

public class WebhookSigningTests
{
	[Test]
	public async Task Sign_OverIdTimestampAndBody_MatchesAnIndependentHmac()
	{
		var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var timestamp = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
		var body = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
		var secret = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));

		var (webhookId, unixTimestamp, signature) = WebhookSigning.Sign(id, timestamp, body, secret);

		await Assert.That(webhookId).IsEqualTo(id.ToString());
		await Assert.That(unixTimestamp).IsEqualTo("1700000000");
		await Assert.That(signature).IsEqualTo(Expected(secret, webhookId, unixTimestamp, body));
	}

	[Test]
	public async Task GenerateSecret_WhenCalled_IsWhsecPrefixedAndDecodesTo32Bytes()
	{
		var secret = WebhookSigning.GenerateSecret();

		await Assert.That(secret).StartsWith("whsec_");
		await Assert.That(Convert.FromBase64String(secret["whsec_".Length..]).Length).IsEqualTo(32);
	}

	private static string Expected(string secret, string id, string timestamp, byte[] body)
	{
		var key = Convert.FromBase64String(secret["whsec_".Length..]);
		var content = Encoding.UTF8.GetBytes($"{id}.{timestamp}.").Concat(body).ToArray();
		return "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, content));
	}
}
