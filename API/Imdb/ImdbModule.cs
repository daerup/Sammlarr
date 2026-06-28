using Contracts.Media;
using Contracts.Media.Conversions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Imdb;

internal class ImdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddKeyedScoped<IExternalIdConverter, ImdbIdConverter>(MediaProvider.Imdb);
}
