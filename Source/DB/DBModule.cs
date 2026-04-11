using DB.Conversion;
using DB.Conversion.Encryption;
using DB.Conversion.Enums;
using DB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace DB;

public class DbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<ConversionModule>();
		services.AddModule<DataAccessModule>();
		services.AddScoped<EntityAuditer>();
		services.AddDbContext<SammlarrDbContext>(
			(sp, options) =>
			{
				var sammlarOptions = sp.GetRequiredService<SammlarOptions>();
				if (string.IsNullOrWhiteSpace(sammlarOptions.ConnectionString))
				{
					throw new InvalidOperationException("ConnectionString in Sammlarr options is not configured.");
				}

				options.UseSqlite(sammlarOptions.ConnectionString);
			}
		);
	}
}
