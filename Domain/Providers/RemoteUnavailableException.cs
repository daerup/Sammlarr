namespace Domain.Providers;

public sealed class RemoteUnavailableException(RemoteService remote, bool sent, Exception? innerException = null)
	: Exception($"{remote.GetDescription()} is temporarily unavailable.", innerException)
{
	public RemoteService Remote { get; } = remote;

	public bool Sent { get; } = sent;

	public Optional<DateTimeOffset> Until { get; init; } = new None();
}
