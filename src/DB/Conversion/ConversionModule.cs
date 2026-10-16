using DB.Conversion.Encryption;
using DB.Conversion.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Conversion;

internal sealed class ConversionModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<EncryptionModule>();
		services.AddModule<EnumModule>();
	}
}
