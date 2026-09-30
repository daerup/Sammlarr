using Domain.Media;

namespace Domain.Wantlist.Outbox;

public enum WantlistOutboxState
{
	Pending,
	Failed,
}

public enum WantlistOperationAction
{
	Add,
	Remove,
}

public record WantlistOutboxEntry(MediaId MediaId, WantlistOperationAction Action, WantlistOutboxState Status, int Attempts)
{
	public DateTimeOffset AddedAt { get; init; }
}
