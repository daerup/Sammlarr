using Contracts.Wantlist.Outbox;
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
		options.UseSystemTextJsonForSerialization(json => json.Converters.Add(new OptionalJsonConverterFactory()));
		options.PersistMessagesWithSqlite(connectionString);
		options.Policies.UseDurableLocalQueues();
		options.Discovery.IncludeAssembly(typeof(MessagingModule).Assembly);
		options.Policies.OnException<NotificationDeliveryFailedException>().RetryWithCooldown(NotificationDeliveryRetry.Cooldowns);
	}
}
