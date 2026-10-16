using System.Diagnostics;

namespace AppHost;

internal static class BrowserLaunchExtensions
{
	public static IResourceBuilder<T> WithBrowserLaunch<T>(this IResourceBuilder<T> builder, string endpointName = "http", TimeSpan? delay = null)
		where T : IResourceWithEndpoints
	{
		return builder.WithBrowserLaunch(() => builder.GetEndpoint(endpointName).Url, delay);
	}

	public static IResourceBuilder<T> WithBrowserLaunch<T>(this IResourceBuilder<T> builder, Func<string> url, TimeSpan? delay = null)
		where T : IResource
	{
		if (!builder.ApplicationBuilder.ExecutionContext.IsRunMode)
			return builder;

		return builder.OnResourceReady(
			async (_, _, cancellationToken) =>
			{
				await Task.Delay(delay ?? TimeSpan.FromSeconds(2), cancellationToken);
				Open(url());
			}
		);
	}

	public static void OpenOnceStarted(this IDistributedApplicationBuilder builder, string url)
	{
		builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(
			(_, _) =>
			{
				Open(url);
				return Task.CompletedTask;
			}
		);
	}

	private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
