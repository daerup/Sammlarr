using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace API.Notifications;

internal sealed class PushoverChannel(PushoverClient client, INotificationTemplateRenderer renderer, ILogger<PushoverChannel> logger) : INotificationChannel
{
	private const string FeedRunFailedType = "feed.run.failed";

	public NotificationChannel Kind => NotificationChannel.Pushover;

	public async Task<DeliveryResult> SendAsync(NotificationContext context, NotificationTarget target, CancellationToken cancellationToken)
	{
		if (target.Config is not PushoverConfig config)
		{
			return new DeliveryPermanent("The target is not a Pushover target.");
		}

		var titleResult = Render(config.TitleTemplate, context, context.Title);
		if (titleResult is not Success<string> title)
		{
			return RenderFailure(titleResult, "title");
		}
		var messageResult = Render(config.MessageTemplate, context, context.Summary);
		if (messageResult is not Success<string> message)
		{
			return RenderFailure(messageResult, "message");
		}

		var request = new PushoverMessage(config.AppToken, config.UserKey, title.Value, message.Value, Priority(context.Type));
		var result = await client.SendAsync(request, cancellationToken);
		if (result is DeliveryPermanent permanent)
		{
			logger.LogWarning("Pushover delivery permanently failed: {Reason}", permanent.Reason);
		}
		return result;
	}

	private Result<string> Render(string? template, NotificationContext context, string fallback) =>
		template is { } value ? renderer.Render(value, context) : Success.Of(fallback);

	private DeliveryResult RenderFailure(Result<string> rendered, string field)
	{
		var reason = rendered is Error error ? error.Reason : $"The Pushover {field} template could not be rendered.";
		logger.LogWarning("Pushover {Field} template could not be rendered: {Reason}", field, reason);
		return new DeliveryPermanent(reason);
	}

	private static int Priority(string type) => type == FeedRunFailedType ? 1 : 0;
}
