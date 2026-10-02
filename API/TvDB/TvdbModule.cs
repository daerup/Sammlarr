using Contracts.Media.Conversions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.TvDB;

internal class TvdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddScoped<IExternalIdConverter, TvdbIdConverter>();
}
