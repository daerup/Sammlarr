using Contracts.Announcing;
using Domain.Announcing;

namespace TestInfrastructure;

public sealed class TestEventPublisher : IEventPublisher
{
	public event Action<IEvent> Published = delegate { };

	public void Publish(IEvent @event) => Published.Invoke(@event);
}
