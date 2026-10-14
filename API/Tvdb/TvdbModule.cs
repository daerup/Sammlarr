using Contracts.Media.Artwork;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Tvdb;

internal sealed class TvdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<IArtworkResizer, TvdbArtwork>();
	}
}
