namespace UI.Shared.Services;

internal class ProfileDropdownService
{
	public event Func<Task>? OnOpenRequested;

	public async Task RequestOpenAsync()
	{
		if (OnOpenRequested is not null)
			await OnOpenRequested.Invoke();
	}
}
