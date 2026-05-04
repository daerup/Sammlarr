using BlazorBlueprint.Components;
using UI.Shared.Common;

namespace UI.Shared.Services;

internal sealed class Notifier(ToastService toastService, DialogService dialogService)
{
	public void Success(string description, string? title = null) => toastService.Success(description, title);

	public void Info(string description, string? title = null) => toastService.Info(description, title);

	public void Warning(string description, string? title = null) => toastService.Warning(description, title);

	public void Error(string description, string? title = null) => toastService.Error(description, title);

	public void Error(Exception exception, string? title = null) =>
		toastService.Show(
			new ToastData
			{
				Title = title,
				Description = $"Exception occured in {exception.Source}",
				Variant = ToastVariant.Destructive,
				Duration = 15000,
				ActionText = "Details",
				Class = "flex-col! items-stretch! space-x-0! gap-2",
				OnAction = () =>
				{
					_ = dialogService.OpenAsync<ExceptionDetailDialog>(
						new Dictionary<string, object?> { { "Exception", exception } },
						new DialogOpenOptions { Title = "Exception Details", Size = DialogSize.ExtraLarge }
					);
				},
			}
		);
}
