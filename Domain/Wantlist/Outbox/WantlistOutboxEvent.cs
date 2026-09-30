using Domain.Announcing;
using Domain.Identity;
using Domain.Media;

namespace Domain.Wantlist.Outbox;

public enum WantlistOutboxEventKind
{
	Pushed,
	Retrying,
	Abandoned,
}

public record WantlistOutboxEvent(UserId UserId, MediaId MediaId, WantlistOutboxEventKind Kind) : IEvent;
