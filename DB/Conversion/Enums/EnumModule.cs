using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Conversion.Enums;

internal sealed class EnumModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddScoped<IPropertyAddedConvention, EnumAsStringConvention>();
}
