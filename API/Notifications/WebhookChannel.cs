using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace API.Notifications;

internal sealed class WebhookChannel(WebhookClient client, INotificationTemplateRenderer renderer, IDateTimeProvider clock, ILogger<WebhookChannel> logger)
	: INotificationChannel
{
	private static readonly JsonSerializerOptions BodyJson = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	public NotificationChannel Kind => NotificationChannel.Webhook;

	public async Task<DeliveryResult> SendAsync(NotificationContext context, NotificationTarget target, CancellationToken cancellationToken)
	{
		if (target.Config is not WebhookConfig config)
		{
			return new DeliveryPermanent("The target is not a webhook.");
		}
		if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
		{
			return new DeliveryPermanent("The webhook URL must be an absolute https URI.");
		}

		var rendered = RenderBody(context, config);
		if (rendered is not Success<string> body)
		{
			var reason = rendered is Error error ? error.Reason : "The webhook body could not be rendered.";
			logger.LogWarning("Webhook body for {Url} could not be rendered: {Reason}", uri, reason);
			return new DeliveryPermanent(reason);
		}

		var payload = Encoding.UTF8.GetBytes(body.Value);
		var (id, timestamp, signature) = WebhookSigning.Sign(context.Id, clock.Now, payload, config.SigningSecret);

		using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new ByteArrayContent(payload) };
		request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
		request.Headers.TryAddWithoutValidation("webhook-id", id);
		request.Headers.TryAddWithoutValidation("webhook-timestamp", timestamp);
		request.Headers.TryAddWithoutValidation("webhook-signature", signature);

		var result = await client.SendAsync(request, cancellationToken);
		if (result is DeliveryPermanent permanent)
		{
			logger.LogWarning("Webhook delivery to {Url} permanently failed: {Reason}", uri, permanent.Reason);
		}
		return result;
	}

	private Result<string> RenderBody(NotificationContext context, WebhookConfig config) =>
		config.BodyTemplate is { } template ? renderer.Render(template, context) : DefaultBody(context);

	private static Result<string> DefaultBody(NotificationContext context) =>
		Success.Of(
			JsonSerializer.Serialize(
				new
				{
					id = context.Id.ToString(),
					type = context.Type,
					time = context.OccurredAt,
					specversion = "1",
					data = context.Data,
				},
				BodyJson
			)
		);
}
