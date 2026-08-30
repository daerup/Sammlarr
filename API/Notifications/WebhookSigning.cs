using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace API.Notifications;

internal static class WebhookSigning
{
	private const string SecretPrefix = "whsec_";

	public static string GenerateSecret() => SecretPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

	public static (string Id, string Timestamp, string Signature) Sign(Guid id, DateTimeOffset timestamp, byte[] body, string signingSecret)
	{
		var webhookId = id.ToString();
		var unixTimestamp = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

		var prefix = Encoding.UTF8.GetBytes($"{webhookId}.{unixTimestamp}.");
		var signed = new byte[prefix.Length + body.Length];
		prefix.CopyTo(signed, 0);
		body.CopyTo(signed, prefix.Length);

		var signature = HMACSHA256.HashData(DecodeSecret(signingSecret), signed);
		return (webhookId, unixTimestamp, $"v1,{Convert.ToBase64String(signature)}");
	}

	private static byte[] DecodeSecret(string signingSecret)
	{
		var raw = signingSecret.StartsWith(SecretPrefix, StringComparison.Ordinal) ? signingSecret[SecretPrefix.Length..] : signingSecret;
		return Convert.FromBase64String(raw);
	}
}
