using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace API.Notifications.Webhook;

internal sealed class WebhookChannel(WebhookClient client, INotificationTemplateRenderer renderer, IDateTimeProvider clock, ILogger<WebhookChannel> logger)
	: INotificationChannel
{
	private static readonly JsonSerializerOptions BodyJson = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() },
	};

	public async Task<DeliveryResult> SendAsync(
		NotificationContext context,
		NotificationTarget target,
		NotificationTemplate template,
		CancellationToken cancellationToken = default
	)
	{
		if (target.Config is not WebhookConfig config)
		{
			return new DeliveryUnrecoverable("The target is not a webhook.");
		}
		return await config
			.Endpoint()
			.Match(
				uri => RenderBody(context, template).Match(body => SendAsync(config, uri, context, body, cancellationToken), error => RenderFailure(error, uri)),
				() => new DeliveryUnrecoverable("The webhook URL must be an absolute http or https URI.")
			);
	}

	private async Task<DeliveryResult> SendAsync(WebhookConfig config, Uri uri, NotificationContext context, string body, CancellationToken cancellationToken)
	{
		var payload = Encoding.UTF8.GetBytes(body);
		var (id, timestamp, signature) = WebhookSigning.Sign(context.Id, clock.Now, payload, config.SigningSecret);

		using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new ByteArrayContent(payload) };
		request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
		request.Headers.TryAddWithoutValidation("webhook-id", id);
		request.Headers.TryAddWithoutValidation("webhook-timestamp", timestamp);
		request.Headers.TryAddWithoutValidation("webhook-signature", signature);

		var result = await client.SendAsync(request, cancellationToken);
		if (result is DeliveryUnrecoverable unrecoverable)
		{
			logger.LogWarning("Webhook delivery to {Url} permanently failed: {Reason}", uri, unrecoverable.Reason);
		}
		return result;
	}

	private DeliveryResult RenderFailure(Error error, Uri uri)
	{
		logger.LogWarning("Webhook body for {Url} could not be rendered: {Reason}", uri, error.Reason);
		return new DeliveryUnrecoverable(error.Reason);
	}

	private Result<string> RenderBody(NotificationContext context, NotificationTemplate template) =>
		renderer.Render(template, context).Map(rendered => rendered.Body.GetValueOr(() => DefaultBody(context)));

	private static string DefaultBody(NotificationContext context) =>
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
		);
}
