using Contracts.Media.Artwork;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Tvdb;

internal class TvdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddKeyedSingleton<IArtworkResizer, TvdbArtwork>(ArtworkProvider.Tvdb);
	}
}
