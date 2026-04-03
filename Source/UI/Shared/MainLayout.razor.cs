using BlazorBlueprint.Components;
using Contracts.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using Shared.DI;

namespace UI.Shared;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
	[Inject]
	private ICredentialStore CredentialStore { get; set; } = null!;

	[Inject]
	private IKeyedServiceIndex<AuthProvider, IOAuthService> OAuthServiceIndex { get; set; } = null!;

	[Inject]
	private NavigationManager NavigationManager { get; set; } = null!;

	[Inject]
	private IJSRuntime Js { get; set; } = null!;

	[Inject]
	private ProfileDropdownService ProfileDropdownService { get; set; } = null!;

	private bool _isAuthenticated;
	private bool _isProcessing;
	private string? _avatarUrl;
	private string? _username;

	private IOAuthService OAuthService => OAuthServiceIndex[AuthProvider.Plex];

	protected override async Task OnInitializedAsync()
	{
		await RefreshAuthStatusAsync();
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			NavigationManager.LocationChanged += HandleLocationChanged;
			ProfileDropdownService.OnOpenRequested += HandleProfileDropdownOpenRequestedAsync;

			StateHasChanged();
		}
	}

	private async void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
	{
		await RefreshAuthStatusAsync();
		await InvokeAsync(StateHasChanged);
	}

	private async Task RefreshAuthStatusAsync()
	{
		var credential = await CredentialStore.GetAsync(AuthProvider.Plex);
		if (credential is null || string.IsNullOrWhiteSpace(credential.AccessToken))
		{
			_isAuthenticated = false;
			_avatarUrl = null;
			_username = null;
			return;
		}

		var userInfo = await OAuthService.GetLoggedInUser();
		_isAuthenticated = userInfo is not null;
		_avatarUrl = userInfo?.AvatarUrl;
		_username = userInfo?.Username;

		if (userInfo is null)
			await CredentialStore.ClearAsync(AuthProvider.Plex);
	}

	private async Task InitiateAuthAsync()
	{
		_isProcessing = true;
		try
		{
			var forwardUrl = NavigationManager.ToAbsoluteUri("/plex-auth?pinId={0}&clientId={1}").ToString();
			var authUrl = await OAuthService.GetOAuthUrl(forwardUrl);
			NavigationManager.NavigateTo(authUrl, forceLoad: true);
		}
		catch
		{
			_isProcessing = false;
		}
	}

	private async Task DisconnectAsync()
	{
		await CredentialStore.ClearAsync(AuthProvider.Plex);
		NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
	}

	private async Task HandleProfileDropdownOpenRequestedAsync()
	{
		_isProfileDropdownOpen = true;
		await InvokeAsync(StateHasChanged);
	}

	public void Dispose()
	{
		NavigationManager.LocationChanged -= HandleLocationChanged;
		ProfileDropdownService.OnOpenRequested -= HandleProfileDropdownOpenRequestedAsync;
	}
}
