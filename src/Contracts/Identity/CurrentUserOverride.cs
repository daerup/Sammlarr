namespace Contracts.Identity;

public sealed class CurrentUserOverride
{
	public Optional<ICurrentUser> Value { get; set; } = new None();
}
