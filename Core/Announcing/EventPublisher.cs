using Contracts.Announcing;
using Domain.Announcing;

namespace Core.Announcing;

internal sealed class EventPublisher : IEventPublisher
{
	public event Action<IEvent> Published = delegate { };

	public void Publish(IEvent @event) => Published.Invoke(@event);
}
