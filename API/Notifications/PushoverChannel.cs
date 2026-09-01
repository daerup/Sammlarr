using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace API.Notifications;

internal sealed class PushoverChannel(PushoverClient client, INotificationTemplateRenderer renderer, ILogger<PushoverChannel> logger) : INotificationChannel
{
	private const string FeedRunFailedType = "feed.run.failed";
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
			return new DeliveryPermanent("The target is not a Pushover target.");
		}

		var titleResult = Render(template.Title, context);
		if (titleResult is not Success<string> title)
		{
			return RenderFailure(titleResult, "title");
		}
		var messageResult = Render(template.Message, context);
		if (messageResult is not Success<string> message)
		{
			return RenderFailure(messageResult, "message");
		}

		var priority = template.Priority is Some<int> configured ? configured.Value : DerivedPriority(context.Type);
		var messageText = string.IsNullOrWhiteSpace(message.Value) ? EmptyMessagePlaceholder : message.Value;
		var request = new PushoverMessage(config.AppToken, config.UserKey, title.Value, messageText, priority);
		var result = await client.SendAsync(request, cancellationToken);
		if (result is DeliveryPermanent permanent)
		{
			logger.LogWarning("Pushover delivery permanently failed: {Reason}", permanent.Reason);
		}
		return result;
	}

	private Result<string> Render(Optional<string> template, NotificationContext context) =>
		template is Some<string> value ? renderer.Render(value.Value, context) : Success.Of(string.Empty);

	private DeliveryResult RenderFailure(Result<string> rendered, string field)
	{
		var reason = rendered is Error error ? error.Reason : $"The Pushover {field} template could not be rendered.";
		logger.LogWarning("Pushover {Field} template could not be rendered: {Reason}", field, reason);
		return new DeliveryPermanent(reason);
	}

	private static int DerivedPriority(string type) => type == FeedRunFailedType ? 1 : 0;
}
