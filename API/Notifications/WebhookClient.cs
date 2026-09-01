using System.Net;
using Domain.Notifications;

namespace API.Notifications;

internal sealed class WebhookClient(HttpClient client)
{
	public async Task<DeliveryResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await client.SendAsync(request, cancellationToken);
			return Map(response.StatusCode);
		}
		catch (HttpRequestException error) when (Blocked(error) is { } blocked)
		{
			return new DeliveryUnrecoverable(blocked.Message);
		}
		catch (HttpRequestException error)
		{
			return new DeliveryRetryable(error.Message);
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return new DeliveryRetryable("The webhook request timed out.");
		}
	}

	private static WebhookBlockedException? Blocked(Exception error) =>
		error switch
		{
			WebhookBlockedException blocked => blocked,
			{ InnerException: { } inner } => Blocked(inner),
			_ => null,
		};

	private static DeliveryResult Map(HttpStatusCode status) =>
		(int)status switch
		{
			>= 200 and < 300 => new DeliverySucceeded(),
			>= 300 and < 400 => new DeliveryUnrecoverable($"The webhook endpoint returned an unfollowed redirect (HTTP {(int)status})."),
			429 => new DeliveryRetryable("The webhook endpoint is rate limiting (HTTP 429)."),
			>= 400 and < 500 => new DeliveryUnrecoverable($"The webhook endpoint returned HTTP {(int)status}."),
			_ => new DeliveryRetryable($"The webhook endpoint returned HTTP {(int)status}."),
		};
}
