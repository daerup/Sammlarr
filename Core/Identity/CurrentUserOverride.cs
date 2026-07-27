using Contracts.Identity;

namespace Core.Identity;

public sealed class CurrentUserOverride
{
	private readonly AsyncLocal<ICurrentUser?> _current = new();

	public ICurrentUser? Value
	{
		get => _current.Value;
		set => _current.Value = value;
	}
}
