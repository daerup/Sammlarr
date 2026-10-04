using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace E2E.Tests;

internal static class StackBoot
{
	internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

	internal static void WaitThroughBusyDocker(this IDistributedApplicationTestingBuilder builder)
	{
		builder.Services.Configure<ResourceNotificationServiceOptions>(options => options.DefaultWaitBehavior = WaitBehavior.WaitOnResourceUnavailable);
	}
}
