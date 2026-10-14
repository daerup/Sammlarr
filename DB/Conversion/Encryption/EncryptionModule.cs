using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DI;
using Shared.Options;

namespace DB.Conversion.Encryption;

internal sealed class EncryptionModule : IServiceModule
{
	private const string ApplicationName = "Sammlarr";

	public void RegisterServices(IServiceCollection services)
	{
		services.AddDataProtection().SetApplicationName(ApplicationName);
		services
			.AddOptions<KeyManagementOptions>()
			.Configure<SammlarOptions, ILoggerFactory>(
				(keys, sammlarr, loggers) => keys.XmlRepository = new FileSystemXmlRepository(DataProtectionKeyRing.Resolve(sammlarr.ConnectionString), loggers)
			);
		services.AddScoped<IDataProtectionService, DataProtectionService>();
		services.AddScoped<IPropertyAddedConvention, EncryptedPropertyConvention>();
	}
}
