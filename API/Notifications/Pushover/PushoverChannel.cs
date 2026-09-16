using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace API.Notifications.Pushover;

internal sealed class PushoverChannel(PushoverClient client, INotificationTemplateRenderer renderer, ILogger<PushoverChannel> logger) : INotificationChannel
{
	private const string EmptyMessagePlaceholder = "(empty)";

	public NotificationChannel Kind => NotificationChannel.Pushover;

	public async Task<DeliveryResult> SendAsync(
		NotificationContext context,
		NotificationTarget target,
		NotificationTemplate template,
		CancellationToken cancellationToken
	)
	{
		if (target.Config is not PushoverConfig config)
		{
			return new DeliveryUnrecoverable("The target is not a Pushover target.");
		}

		return await renderer.Render(template, context).Match(RenderSuccess, RenderFailure);

		async Task<DeliveryResult> RenderSuccess(RenderedNotification content) => await SendAsync(config, context, template, content, cancellationToken);
	}

	private async Task<DeliveryResult> SendAsync(
		PushoverConfig config,
		NotificationContext context,
		NotificationTemplate template,
		RenderedNotification content,
		CancellationToken cancellationToken
	)
	{
		var priority = template.Priority.GetValueOr(() => DerivedPriority(context.Type));
		var message = content.Message.GetValueOr(string.Empty);
		var messageText = string.IsNullOrWhiteSpace(message) ? EmptyMessagePlaceholder : message;
		var request = new PushoverMessage(config.AppToken, config.UserKey, content.Title.GetValueOr(string.Empty), messageText, priority);
		var result = await client.SendAsync(request, cancellationToken);
		if (result is DeliveryUnrecoverable unrecoverable)
		{
			logger.LogWarning("Pushover delivery permanently failed: {Reason}", unrecoverable.Reason);
		}
		return result;
	}

	private Task<DeliveryResult> RenderFailure(Error error)
	{
		logger.LogWarning("Pushover template could not be rendered: {Reason}", error.Reason);
		return Task.FromResult<DeliveryResult>(new DeliveryUnrecoverable(error.Reason));
	}

	private static int DerivedPriority(NotificationEventType type) => type == NotificationEventType.FeedRunFailed ? 1 : 0;
}
