using Contracts.Identity;

namespace Core.Identity;

public sealed class CurrentUserOverride
{
	public ICurrentUser? Value { get; set; }
}
