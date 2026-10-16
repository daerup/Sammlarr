using Contracts.Messaging;

namespace Harness;

internal sealed class PublishedMessages
{
	private readonly List<object> _messages = [];
	private readonly Lock _gate = new();

	internal void Record(object message)
	{
		lock (_gate)
		{
			_messages.Add(message);
		}
	}

	internal IReadOnlyList<T> Recorded<T>()
	{
		lock (_gate)
		{
			return [.. _messages.OfType<T>()];
		}
	}
}

internal sealed class RecordingMessagePublisher(PublishedMessages published) : IMessagePublisher
{
	public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
		where TMessage : notnull
	{
		published.Record(message);
		return Task.CompletedTask;
	}
}
