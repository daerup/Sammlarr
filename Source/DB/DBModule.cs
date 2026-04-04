using Contracts.Auth;
using Contracts.Media;
using DB.Data;
using DB.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB;

public class DbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddMemoryCache();

		services.AddDbContext<SammlarrDbContext>(
			(sp, options) =>
			{
				var configuration = sp.GetRequiredService<IConfiguration>();
				var connectionString =
					configuration.GetConnectionString("Sammlarr") ?? throw new InvalidOperationException("Connection string 'Sammlarr' is not configured.");

				options.UseSqlite(connectionString);
			}
		);

		services.AddScoped<ICredentialStore, CredentialStore>();
		services.AddScoped<IApiKeyStore, ApiKeyStore>();
		services.AddScoped<IMediaIdStore, MediaIdRepository>();
	}
}
