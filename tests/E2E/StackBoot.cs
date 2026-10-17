using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace E2E.Tests;

internal static class StackBoot
{
	internal const string ArrApiKey = "0123456789abcdef0123456789abcdef";

	internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

	internal static void WaitThroughBusyDocker(this IDistributedApplicationTestingBuilder builder)
	{
		builder.Services.Configure<ResourceNotificationServiceOptions>(options => options.DefaultWaitBehavior = WaitBehavior.WaitOnResourceUnavailable);
	}

	internal static void RunProjectsOutsideTheIde(this IDistributedApplicationTestingBuilder builder)
	{
		builder.Configuration["DEBUG_SESSION_PORT"] = "";
	}

	internal static IResourceBuilder<ProjectResource> WithoutTelemetryExport(this IResourceBuilder<ProjectResource> web)
	{
		return web.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "");
	}

	internal static IReadOnlyList<IResourceBuilder<ContainerResource>> UnpinArrPorts(this IDistributedApplicationTestingBuilder builder)
	{
		return
		[
			.. builder
				.Resources.OfType<ContainerResource>()
				.Where(resource => resource.Name is "radarr" or "sonarr")
				.ToList()
				.Select(arr => builder.CreateResourceBuilder(arr).WithEndpoint("http", endpoint => endpoint.Port = null)),
		];
	}
}
