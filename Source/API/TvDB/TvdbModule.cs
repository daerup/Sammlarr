using API.TMDB;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Trending;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.TvDB;

internal class TvdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<TvdbIdConverter>();
	}
}
