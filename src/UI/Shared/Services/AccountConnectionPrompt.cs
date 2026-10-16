using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace UI.Shared.Services;

internal sealed class AccountConnectionPrompt(
	AuthenticationStateProvider authenticationStateProvider,
	NavigationManager navigationManager,
	ProfileDropdownService profileDropdownService
)
{
	public async Task RequestAsync()
	{
		var state = await authenticationStateProvider.GetAuthenticationStateAsync();
		if (state.User.Identity?.IsAuthenticated == true)
		{
			navigationManager.NavigateTo("/settings?section=providers");
			return;
		}

		await profileDropdownService.RequestOpenAsync();
	}
}
