namespace Domain.Providers;

public sealed class RemoteUnavailableException(RemoteService remote, Exception? innerException = null)
	: Exception($"{remote.GetDescription()} is temporarily unavailable.", innerException)
{
	public RemoteService Remote { get; } = remote;
}
