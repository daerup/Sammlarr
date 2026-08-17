using Contracts.Messaging;
using Wolverine;

namespace Messaging;

internal sealed class WolverineMessagePublisher(IMessageBus bus) : IMessagePublisher
{
	public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
		where TMessage : notnull => await bus.PublishAsync(message);
}
