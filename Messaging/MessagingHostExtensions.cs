using System.Runtime.CompilerServices;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Messaging.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shared.Options;
using Weasel.Sqlite;
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
		options.ApplicationAssembly = typeof(MessagingModule).Assembly;
		options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
		options.CodeGeneration.GeneratedCodeOutputPath = Path.Combine(SourceFolder(), "Internal", "Generated");
		options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
		options.UseSystemTextJsonForSerialization(json => json.Converters.Add(new OptionalJsonConverterFactory()));
		options.PersistMessagesWithSqlite(new SqliteDataSource(connectionString, new SqlitePragmaSettings { MmapSize = 0 }));
		options.Durability.DeadLetterQueueExpirationEnabled = true;
		options.Policies.UseDurableLocalQueues();
		options.Policies.OnException<NotificationDeliveryFailedException>().RetryWithCooldown(NotificationDeliveryRetry.Cooldowns);
	}

	private static string SourceFolder([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;
}
