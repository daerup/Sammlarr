using Contracts;
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
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (options.ResetAdminPassword.Length == 0)
		{
			return;
		}

		await using var scope = services.CreateAsyncScope();
		await Passwords.Weakness(options.ResetAdminPassword).Match(Refuse, () => ResetAsync(scope.ServiceProvider, cancellationToken));
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private Task Refuse(Error reason)
	{
		logger.LogError("Sammlarr:Accounts:ResetAdminPassword was not applied: {Reason}", reason.Reason);
		return Task.CompletedTask;
	}

	private async Task ResetAsync(IServiceProvider scoped, CancellationToken cancellationToken)
	{
		var config = await scoped.GetRequiredService<IProviderConfigService>().GetAsync(cancellationToken);
		if (config.AuthProvider != AuthProvider.Local)
		{
			logger.LogError("Sammlarr:Accounts:ResetAdminPassword was not applied: this Sammlarr signs in with {Provider}", config.AuthProvider);
			return;
		}

		var accounts = scoped.GetRequiredService<ILocalAccountRepository>();
		var admin = (await accounts.GetAllAsync(cancellationToken)).FirstOrNone(account => account.User.Role == UserRole.Admin);
		await admin.Match(
			found => ApplyAsync(scoped, accounts, found, cancellationToken),
			() => Refuse(Error.From("there is no admin with a Sammlarr account yet"))
		);
	}

	private async Task ApplyAsync(IServiceProvider scoped, ILocalAccountRepository accounts, LocalAccount admin, CancellationToken cancellationToken)
	{
		if (admin.ConfigResetHash.Match(hash => Passwords.Verify(hash, options.ResetAdminPassword), () => false))
		{
			logger.LogWarning("Sammlarr:Accounts:ResetAdminPassword was already applied to admin {Username}. Remove the setting now.", admin.Username);
			return;
		}

		var now = scoped.GetRequiredService<IDateTimeProvider>().Now;
		await accounts.ResetFromConfigAsync(admin.User.Id, Passwords.Hash(options.ResetAdminPassword), now, cancellationToken);
		logger.LogWarning("The password of admin {Username} was reset from Sammlarr:Accounts:ResetAdminPassword. Remove the setting now.", admin.Username);
	}
}
