using Contracts.Auth;
using Contracts.DI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using UI.Shared.Services;

namespace UI.Shared.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
	[Inject]
	private IServiceScopeFactory ScopeFactory { get; set; } = null!;

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
	private readonly CancellationTokenSource _disposeCts = new();
	private bool _isDisposed;

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

	private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
	{
		_ = HandleLocationChangedAsync();
	}

	private async Task HandleLocationChangedAsync()
	{
		if (_isDisposed || _disposeCts.IsCancellationRequested)
		{
			return;
		}

		await RefreshAuthStatusAsync(_disposeCts.Token);

		if (_isDisposed || _disposeCts.IsCancellationRequested)
		{
			return;
		}

		await InvokeAsync(StateHasChanged);
	}

	private async Task RefreshAuthStatusAsync(CancellationToken cancellationToken = default)
	{
		await using var scope = ScopeFactory.CreateAsyncScope();
		var credentialStore = scope.ServiceProvider.GetRequiredService<ICredentialStore>();
		var oauthServiceIndex = scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<WatchlistProvider, IOAuthService>>();
		var oauthService = oauthServiceIndex[WatchlistProvider.Plex];

		var credential = await credentialStore.GetAsync(WatchlistProvider.Plex, cancellationToken);
		if (credential is null || string.IsNullOrWhiteSpace(credential.AccessToken))
		{
			_isAuthenticated = false;
			_avatarUrl = null;
			_username = null;
			return;
		}

		var userInfo = await oauthService.GetLoggedInUser();
		_isAuthenticated = userInfo is not null;
		_avatarUrl = userInfo?.AvatarUrl;
		_username = userInfo?.Username;

		if (userInfo is null)
		{
			await credentialStore.ClearAsync(WatchlistProvider.Plex, cancellationToken);
		}
	}

	private async Task InitiateAuthAsync()
	{
		_isProcessing = true;
		try
		{
			await using var scope = ScopeFactory.CreateAsyncScope();
			var oauthServiceIndex = scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<WatchlistProvider, IOAuthService>>();
			var oauthService = oauthServiceIndex[WatchlistProvider.Plex];

			var returnUrl = GetReturnUrlForOAuth();
			var escapedReturnUrl = Uri.EscapeDataString(returnUrl);
			var forwardUrl = NavigationManager.ToAbsoluteUri($"/plex-auth?pinId={{0}}&clientId={{1}}&returnUrl={escapedReturnUrl}").ToString();
			var authUrl = await oauthService.GetOAuthUrl(forwardUrl, _disposeCts.Token);
			NavigationManager.NavigateTo(authUrl, forceLoad: true);
		}
		catch
		{
			_isProcessing = false;
		}
	}

	private string GetReturnUrlForOAuth()
	{
		var relative = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
		if (string.IsNullOrWhiteSpace(relative))
		{
			return "/";
		}

		if (relative.StartsWith("plex-auth", StringComparison.OrdinalIgnoreCase))
		{
			return "/";
		}

		return relative.StartsWith('/') ? relative : $"/{relative}";
	}

	private async Task DisconnectAsync()
	{
		await using var scope = ScopeFactory.CreateAsyncScope();
		var credentialStore = scope.ServiceProvider.GetRequiredService<ICredentialStore>();
		await credentialStore.ClearAsync(WatchlistProvider.Plex, _disposeCts.Token);
		NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
	}

	private async Task HandleProfileDropdownOpenRequestedAsync()
	{
		_isProfileDropdownOpen = true;
		await InvokeAsync(StateHasChanged);
	}

	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}

		_isDisposed = true;
		_disposeCts.Cancel();
		_disposeCts.Dispose();
		NavigationManager.LocationChanged -= HandleLocationChanged;
		ProfileDropdownService.OnOpenRequested -= HandleProfileDropdownOpenRequestedAsync;
	}
}
