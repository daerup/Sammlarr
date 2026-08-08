using System.Diagnostics;

namespace AppHost;

internal static class BrowserLaunchExtensions
{
	public static IResourceBuilder<T> WithBrowserLaunch<T>(this IResourceBuilder<T> builder, string endpointName = "http", TimeSpan? delay = null)
		where T : IResourceWithEndpoints
	{
		if (!builder.ApplicationBuilder.ExecutionContext.IsRunMode)
			return builder;

		return builder.OnResourceReady(
			async (_, _, cancellationToken) =>
			{
				await Task.Delay(delay ?? TimeSpan.FromSeconds(2), cancellationToken);
				Process.Start(new ProcessStartInfo(builder.GetEndpoint(endpointName).Url) { UseShellExecute = true });
			}
		);
	}
}
