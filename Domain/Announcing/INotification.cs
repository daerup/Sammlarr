using Domain.Identity;

namespace Domain.Announcing;

public interface INotification
{
	UserId UserId { get; }
}
