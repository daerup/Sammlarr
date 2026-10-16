using Contracts.Announcing;
using Contracts.Messaging;
using Domain.Announcing;

namespace Core.Announcing;

internal sealed class Announcer(IEventPublisher events, IMessagePublisher notifications)
{
	internal void Announce(IEvent @event) => events.Publish(@event);

	internal async Task AnnounceAsync(INotification notification, CancellationToken cancellationToken) =>
		await notifications.PublishAsync(notification, cancellationToken);
}
