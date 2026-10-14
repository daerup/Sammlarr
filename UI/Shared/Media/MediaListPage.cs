using Contracts.Settings;
using Domain.Settings;
using Microsoft.AspNetCore.Components;
using UI.Shared.Filters;
using UI.Shared.Services;

namespace UI.Shared.Media;

public abstract class MediaListPage : ComponentBase, IDisposable
{
	[Inject]
	protected IProviderConfigService ProviderConfigService { get; set; } = null!;

	[Inject]
	internal Notifier Notifier { get; set; } = null!;

	[Inject]
	internal ScrollService ScrollService { get; set; } = null!;

	private const int PageSize = 20;

	private int _page = 1;
	protected readonly CancellationTokenSource Cancellation = new();
	protected ProviderConfig? Settings;
	protected ListFilter Filter = ListFilter.None;
	protected bool ShowFilters;
	protected bool IsLoadingList;
	protected string? ErrorMessage;
	protected bool IsEditMode;
	protected bool IsSheetOpen;

	private bool signedIn;
	private bool connected;

	protected abstract Task LoadAsync();

	protected abstract void ClearItems();

	protected abstract void ClearSelection();

	protected bool IsSignedIn() => signedIn;

	protected bool IsConnected() => connected;

	protected override async Task OnInitializedAsync() =>
		Settings = await ProviderConfigService.GetAsync(Cancellation.Token) ?? throw new InvalidOperationException("Provider settings are not configured.");

	protected void HandleAccessChanged(bool hasAccess)
	{
		connected = hasAccess;
		StateHasChanged();
	}

	protected async Task HandleSignInChanged(bool hasAccess)
	{
		signedIn = hasAccess;

		if (!hasAccess)
		{
			ClearItems();
			ErrorMessage = null;
			IsLoadingList = false;
			IsEditMode = false;
			CloseSheet();
			return;
		}

		IsLoadingList = true;
		StateHasChanged();
		await Task.Yield();
		await LoadAsync();
	}

	protected void ToggleFilterBar() => ShowFilters = !ShowFilters;

	protected Task ApplyFilterAsync(ListFilter filter)
	{
		Filter = filter;
		_page = 1;
		return Task.CompletedTask;
	}

	protected static int PageCount(int itemCount) => Math.Max(1, (int)Math.Ceiling(itemCount / (double)PageSize));

	protected int PageWithin(int itemCount) => Math.Clamp(_page, 1, PageCount(itemCount));

	protected IReadOnlyList<T> Paged<T>(IReadOnlyList<T> items) => [.. items.Skip((PageWithin(items.Count) - 1) * PageSize).Take(PageSize)];

	protected Task GoToPageAsync(int page)
	{
		_page = page;
		return ScrollService.ToTopAfterAsync(() => Task.CompletedTask, Cancellation.Token);
	}

	protected void EnterEditMode()
	{
		IsEditMode = true;
		CloseSheet();
	}

	protected void SetEditMode(bool editing) => IsEditMode = editing;

	protected Task ApplySearchAsync(string? search) => ApplyFilterAsync(Filter with { Search = search });

	private void CloseSheet()
	{
		IsSheetOpen = false;
		ClearSelection();
	}

	protected Task SheetOpenChangedAsync(bool isOpen)
	{
		if (!isOpen)
		{
			CloseSheet();
		}
		return Task.CompletedTask;
	}

	public virtual void Dispose()
	{
		Cancellation.Cancel();
		Cancellation.Dispose();
	}
}
