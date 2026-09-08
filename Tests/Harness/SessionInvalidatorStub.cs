using Contracts.Identity;
using Domain.Identity;

namespace Harness;

internal sealed class SessionInvalidatorStub : ISessionInvalidator
{
	private readonly List<UserId> _invalidated = [];

	internal IReadOnlyList<UserId> Invalidated => _invalidated;

	public void Invalidate(UserId userId) => _invalidated.Add(userId);
}
