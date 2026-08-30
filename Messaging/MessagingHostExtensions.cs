using Contracts.Watchlist.Outbox;
using JasperFx.CodeGeneration.Model;
using Messaging.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shared.Options;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Sqlite;

namespace Messaging;

public static class MessagingHostExtensions
{
	extension(IHostBuilder host)
	{
		public IHostBuilder UseSammlarrMessaging(IConfiguration configuration)
		{
			var connectionString =
				configuration.GetSection(SammlarOptions.SectionName)[nameof(SammlarOptions.ConnectionString)]
				?? throw new InvalidOperationException("Sammlarr:ConnectionString is not configured.");

			return host.UseWolverine(options => Configure(options, connectionString));
		}
	}

	private static void Configure(WolverineOptions options, string connectionString)
	{
		options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
		options.PersistMessagesWithSqlite(connectionString);
		options.Policies.UseDurableLocalQueues();
		options.Discovery.IncludeAssembly(typeof(MessagingModule).Assembly);
		options.LocalQueueFor<WatchlistPushRequest>().Sequential();

		options
			.Policies.OnException<WatchlistPushFailedException>()
			.RetryWithCooldown(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));

		options
			.Policies.OnException<NotificationDeliveryFailedException>()
			.RetryWithCooldown(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1));
	}
}
