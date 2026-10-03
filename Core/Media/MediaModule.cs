using Contracts.Media;
using Contracts.Media.Search;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Media;

internal class MediaModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<MediaIdResolver>();
		services.AddScoped<IUnmatchedMediaCache, UnmatchedMediaCache>();
		services.AddScoped<DetailsFetcher>();
		services.AddScoped<MediaIdentityCoordinator>();
		services.AddScoped<MediaWriteBufferFactory>();
		services.AddTracedScoped<IMediaService, MediaService>();
		services.AddTracedScoped<IMediaSearchService, MediaSearchService>();
	}
}
