using Contracts.Identity;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging;
using UI.Shared.Services;

namespace UI.Shared.Notifications.WebUi;

internal sealed class WebUiChannel(WebUiCircuits circuits, ICurrentUser recipient, INotificationTemplateRenderer renderer, ILogger<WebUiChannel> logger)
	: INotificationChannel
{
	public NotificationChannel Kind => NotificationChannel.WebUi;

	public Task<DeliveryResult> SendAsync(
		NotificationContext context,
		NotificationTarget target,
		NotificationTemplate template,
		CancellationToken cancellationToken = default
	)
	{
		if (target.Config is not WebUiConfig)
		{
			return Task.FromResult<DeliveryResult>(new DeliveryUnrecoverable("The target is not a web UI target."));
		}

		return Task.FromResult(renderer.Render(template, context).Match(rendered => Show(context, template, rendered), RenderFailure));
	}

	private DeliveryResult Show(NotificationContext context, NotificationTemplate template, RenderedNotification rendered)
	{
		circuits.Show(
			recipient.Id,
			new Toast(
				template.Level.GetValueOr(() => NotificationLevel.For(context.Type)),
				rendered.Title.GetValueOr(context.Title),
				rendered.Message.GetValueOr(context.Summary)
			)
		);
		return new DeliverySucceeded();
	}

	private DeliveryResult RenderFailure(Error error)
	{
		logger.LogWarning("Web UI notification could not be rendered: {Reason}", error.Reason);
		return new DeliveryUnrecoverable(error.Reason);
	}
}
