using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shared.Options;
using Wolverine;
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
		options.PersistMessagesWithSqlite(connectionString);
		options.Policies.UseDurableLocalQueues();
		options.Discovery.IncludeAssembly(typeof(MessagingModule).Assembly);
	}
}
