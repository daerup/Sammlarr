using Domain.Announcing;

namespace Contracts.Announcing;

public interface IEventPublisher
{
	event Action<IEvent> Published;

	void Publish(IEvent @event);
}
