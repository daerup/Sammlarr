using DB.Conversion;
using DB.Data;
using DB.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DI;
using Shared.Options;

namespace DB;

public class DbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<ConversionModule>();
		services.AddModule<DataAccessModule>();
		services.AddModule<InstrumentationModule>();
		services.AddScoped<EntityAuditer>();
		services.AddScoped<UserOwnershipStamper>();
		services.AddDbContextFactory<SammlarrDbContext>(
			(sp, options) =>
			{
				var sammlarOptions = sp.GetRequiredService<SammlarOptions>();
				if (string.IsNullOrWhiteSpace(sammlarOptions.ConnectionString))
				{
					throw new InvalidOperationException("ConnectionString in Sammlarr options is not configured.");
				}

				options.UseSqlite(sammlarOptions.ConnectionString);
				options.AddInterceptors(new SqlitePragmaInterceptor());
				options.ConfigureWarnings(w => w.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)));
			},
			lifetime: ServiceLifetime.Scoped
		);
		services.AddHostedService<DbInitializer>();
	}
}
