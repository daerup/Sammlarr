using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Conversion.Encryption;

internal class EncryptionModule : IServiceModule
{
	private const string ApplicationName = "Sammlarr";

	public void RegisterServices(IServiceCollection services)
	{
		services.AddDataProtection().PersistKeysToFileSystem(DataProtectionKeyRing.Resolve()).SetApplicationName(ApplicationName);
		services.AddScoped<IDataProtectionService, DataProtectionService>();
		services.AddScoped<IPropertyAddedConvention, EncryptedPropertyConvention>();
	}
}
