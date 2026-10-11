using Contracts.Media;
using Contracts.Media.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
		services.AddScoped<MediaWriteBufferFactory>();
		services.AddScoped<TitleIdentifier>();
		services.AddScoped<ListLoad>();
		services.TryAddSingleton(TimeProvider.System);
		services.AddTracedScoped<IMediaService, MediaService>();
		services.AddTracedScoped<IMediaSearchService, MediaSearchService>();
	}
}
