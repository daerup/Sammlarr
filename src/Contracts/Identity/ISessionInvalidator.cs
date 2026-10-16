using Domain.Identity;

namespace Contracts.Identity;

public interface ISessionInvalidator
{
	void Invalidate(UserId userId);
}
