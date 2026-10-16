using Contracts.Messaging;

namespace TestInfrastructure;

public sealed class RecordingPublisher : IMessagePublisher
{
	private readonly List<object> _published = [];

	public IReadOnlyList<T> Published<T>() => [.. _published.OfType<T>()];

	public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
		where TMessage : notnull
	{
		_published.Add(message);
		return Task.CompletedTask;
	}
}
