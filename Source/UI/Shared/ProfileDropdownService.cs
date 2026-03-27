namespace UI.Shared;

/// <summary>Allows any page to programmatically open the profile dropdown in the header.</summary>
public sealed class ProfileDropdownService
{
	public event Func<Task>? OnOpenRequested;

	public async Task RequestOpenAsync()
	{
		if (OnOpenRequested is not null)
			await OnOpenRequested.Invoke();
	}
}
