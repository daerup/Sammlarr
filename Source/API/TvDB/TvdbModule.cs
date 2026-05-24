using Contracts.Media;
using Contracts.Media.Convertions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.TvDB;

internal class TvdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddKeyedScoped<IExternalIdConverter, TvdbIdConverter>(MediaProvider.Tvdb);
}
