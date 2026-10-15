using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace Core.Auth;

internal sealed class AdminPasswordReset(IServiceProvider services, AccountsOptions options, ILogger<AdminPasswordReset> logger) : IHostedService
{
	private static readonly Error NoAdmin = Error.From("there is no admin with a Sammlarr account yet");

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (options.ResetAdminPassword.Length == 0)
		{
			return;
		}

		await using var scope = services.CreateAsyncScope();
		var reset = await ResetAsync(scope.ServiceProvider, cancellationToken);
		reset.ApplyError(Refuse);
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private void Refuse(Error reason) => logger.LogError("Sammlarr:Accounts:ResetAdminPassword was not applied: {Reason}", reason.Reason);

	private async Task<Result<ConfigReset>> ResetAsync(IServiceProvider scoped, CancellationToken cancellationToken)
	{
		var config = await scoped.GetRequiredService<IProviderConfigService>().GetAsync(cancellationToken);
		if (config.AuthProvider != AuthProvider.Local)
		{
			return Error.From($"this Sammlarr signs in with {config.AuthProvider}");
		}

		var accounts = scoped.GetRequiredService<ILocalAccountRepository>();
		var admin = (await accounts.GetAllAsync(cancellationToken)).FirstOrNone(account => account.User.Role == UserRole.Admin);
		return await admin.ToResult(NoAdmin).Map(found => ApplyAsync(accounts, found, cancellationToken));
	}

	private async Task<Result<ConfigReset>> ApplyAsync(ILocalAccountRepository accounts, LocalAccount admin, CancellationToken cancellationToken)
	{
		var reset = await accounts.ResetFromConfigAsync(admin.User.Id, options.ResetAdminPassword, cancellationToken);
		reset.Apply(outcome => Report(outcome, admin.Username));
		return reset;
	}

	private void Report(ConfigReset outcome, string username)
	{
		switch (outcome)
		{
			case ConfigReset.AlreadyApplied:
				logger.LogWarning("Sammlarr:Accounts:ResetAdminPassword was already applied to admin {Username}. Remove the setting now.", username);
				break;
			case ConfigReset.Applied:
				logger.LogWarning("The password of admin {Username} was reset from Sammlarr:Accounts:ResetAdminPassword. Remove the setting now.", username);
				break;
		}
	}
}
