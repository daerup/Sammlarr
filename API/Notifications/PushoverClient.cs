using System.Net;
using Domain.Notifications;

namespace API.Notifications;

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
			return Map(response);
		}
		catch (HttpRequestException error)
		{
			return new DeliveryTransient(error.Message);
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return new DeliveryTransient("The Pushover request timed out.");
		}
	}

	private static DeliveryResult Map(HttpResponseMessage response)
	{
		var status = (int)response.StatusCode;
		if (status is >= 200 and < 300)
		{
			return new DeliverySucceeded();
		}
		if (status == (int)HttpStatusCode.TooManyRequests)
		{
			var remaining = response.Headers.TryGetValues("X-Limit-App-Remaining", out var values) ? values.FirstOrDefault() : null;
			return new DeliveryPermanent($"Pushover monthly message limit reached (HTTP 429; app remaining {remaining ?? "0"}).");
		}
		if (status is >= 400 and < 500)
		{
			return new DeliveryPermanent($"Pushover rejected the message (HTTP {status}).");
		}
		return new DeliveryTransient($"Pushover returned HTTP {status}.");
	}
}
