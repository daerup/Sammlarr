using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Encryption;

public class EncryptionModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddDataProtection();
		services.AddScoped<IDataProtectionService, DataProtectionService>();
		services.AddScoped<IPropertyAddedConvention, EncryptedPropertyConvention>();
	}
}
