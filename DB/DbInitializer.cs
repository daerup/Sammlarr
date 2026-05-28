using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DB;

internal sealed class DbInitializer(IServiceProvider serviceProvider) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		await using var scope = serviceProvider.CreateAsyncScope();
		var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SammlarrDbContext>>();
		await using var ctx = await factory.CreateDbContextAsync(cancellationToken);
		ctx.Initialize();
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
