using Domain.Identity;

namespace Domain.Announcing;

public interface IEvent
{
	UserId UserId { get; }
}
