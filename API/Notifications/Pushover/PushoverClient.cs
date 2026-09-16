using System.Net;
using Contracts.Notifications;

namespace API.Notifications.Pushover;

internal sealed record PushoverMessage(string AppToken, string UserKey, string Title, string Message, int Priority);

internal sealed class PushoverClient(HttpClient client)
{
	private const string Endpoint = "https://api.pushover.net/1/messages.json";

	public async Task<DeliveryResult> SendAsync(PushoverMessage message, CancellationToken cancellationToken)
	{
		using var content = new FormUrlEncodedContent(
			new Dictionary<string, string>
			{
				["token"] = message.AppToken,
				["user"] = message.UserKey,
				["title"] = message.Title,
				["message"] = message.Message,
				["priority"] = message.Priority.ToString(),
			}
		);

		try
		{
			using var response = await client.PostAsync(Endpoint, content, cancellationToken);
			return await MapAsync(response, cancellationToken);
		}
		catch (HttpRequestException error)
		{
			return new DeliveryRetryable(error.Message);
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return new DeliveryRetryable("The Pushover request timed out.");
		}
	}

	private static async Task<DeliveryResult> MapAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var status = (int)response.StatusCode;
		if (status is >= 200 and < 300)
		{
			return new DeliverySucceeded();
		}
		var body = response.Content is { } content ? await content.ReadAsStringAsync(cancellationToken) : string.Empty;
		if (status == (int)HttpStatusCode.TooManyRequests)
		{
			var remaining = response.Headers.TryGetValues("X-Limit-App-Remaining", out var values) ? values.FirstOrDefault() : null;
			return new DeliveryUnrecoverable($"Pushover monthly message limit reached (HTTP 429; app remaining {remaining ?? "0"}).");
		}
		if (status is >= 400 and < 500)
		{
			return new DeliveryUnrecoverable($"Pushover rejected the message (HTTP {status}){Detail(body)}");
		}
		return new DeliveryRetryable($"Pushover returned HTTP {status}{Detail(body)}");
	}

	private static string Detail(string body)
	{
		var trimmed = body.Trim();
		return trimmed.Length == 0 ? "." : $": {(trimmed.Length <= 300 ? trimmed : trimmed[..300] + "…")}";
	}
}
