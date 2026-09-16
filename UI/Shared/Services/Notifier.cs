using System.Runtime.CompilerServices;
using BlazorBlueprint.Components;
using Domain.Notifications;
using Microsoft.Extensions.Logging;
using UI.Shared.Common;

namespace UI.Shared.Services;

internal sealed class Notifier(ToastService toastService, DialogService dialogService, ILogger<Notifier> logger)
{
	public void Show(Toast toast, [CallerFilePath] string source = "", [CallerMemberName] string member = "") =>
		Show(toast.Level, toast.Message, toast.Title, source, member);

	public void Show(
		NotificationLevel level,
		string description,
		string? title = null,
		[CallerFilePath] string source = "",
		[CallerMemberName] string member = ""
	)
	{
		switch (level)
		{
			case NotificationLevel.Info:
				Info(description, title, source, member);
				break;
			case NotificationLevel.Warning:
				Warning(description, title, source, member);
				break;
			case NotificationLevel.Error:
				Error(description, title, source, member);
				break;
		}
	}

	public void Success(string description, string? title = null, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
	{
		logger.LogDebug("{Source}.{Member} {Title}: {Description}", Component(source), member, title ?? "Success", description);
		toastService.Success(description, title);
	}

	public void Info(string description, string? title = null, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
	{
		logger.LogDebug("{Source}.{Member} {Title}: {Description}", Component(source), member, title ?? "Info", description);
		toastService.Info(description, title);
	}

	public void Warning(string description, string? title = null, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
	{
		logger.LogWarning("{Source}.{Member} {Title}: {Description}", Component(source), member, title ?? "Warning", description);
		toastService.Show(description, title, ToastVariant.Warning, duration: 20000);
	}

	public void Error(string description, string? title = null, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
	{
		logger.LogError("{Source}.{Member} {Title}: {Description}", Component(source), member, title ?? "Error", description);
		toastService.Show(
			new ToastData
			{
				Title = title,
				Description = description,
				Variant = ToastVariant.Destructive,
				Duration = 20000,
				Class = "toast-soft-destructive",
			}
		);
	}

	public void Error(Exception exception, string? title = null, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
	{
		logger.LogError(exception, "{Source}.{Member} {Title}", Component(source), member, title ?? "Unhandled exception surfaced to user");
		toastService.Show(
			new ToastData
			{
				Title = title,
				Description = $"Exception occured in {exception.Source}",
				Variant = ToastVariant.Destructive,
				Duration = 20000,
				Class = "toast-soft-destructive",
				ActionText = "View details",
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

	private static string Component(string sourcePath) => Path.GetFileName(sourcePath).Split('.', 2)[0];
}
