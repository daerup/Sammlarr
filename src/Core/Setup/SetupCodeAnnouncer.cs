using Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Setup;

internal sealed class SetupCodeAnnouncer(IServiceProvider services, SetupGate gate, ILoggerFactory loggers) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		await using var scope = services.CreateAsyncScope();
		if (await scope.ServiceProvider.GetRequiredService<IUserRepository>().CountAsync(cancellationToken) > 0)
		{
			gate.SawAdmin();
			return;
		}

		loggers.CreateLogger("Sammlarr.Onboarding").LogWarning("No admin yet. Open /setup and enter setup code {SetupCode}", gate.Code.Reveal());
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
