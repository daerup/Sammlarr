using Contracts.Setup;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Setup;

internal sealed class SetupModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<SetupGate>();
		services.AddTracedScoped<IOnboardingService, OnboardingService>();
		services.AddHostedService<SetupCodeAnnouncer>();
	}
}
