namespace Contracts.Identity;

public sealed class CurrentUserOverride
{
	public ICurrentUser? Value { get; set; }
}
