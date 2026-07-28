using System.IO;
using System.Runtime.CompilerServices;
using BlazorBlueprint.Components;
using Domain.Media;
using Microsoft.Extensions.Logging;
using UI.Shared.Common;

namespace UI.Shared.Services;

internal sealed class Notifier(ToastService toastService, DialogService dialogService, ILogger<Notifier> logger)
{
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
		toastService.Show(description, title, ToastVariant.Destructive, duration: 20000);
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
				ActionText = "Details",
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
