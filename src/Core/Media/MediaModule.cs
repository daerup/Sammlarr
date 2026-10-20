using Contracts.Housekeeping;
using Contracts.Media;
using Contracts.Media.Search;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Media;

internal sealed class MediaModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<MediaIdResolver>();
		services.AddScoped<IUnmatchedMediaCache, UnmatchedMediaCache>();
		services.AddScoped<DetailsFetcher>();
		services.AddScoped<LibraryWriterFactory>();
		services.AddScoped<TitleIdentifier>();
		services.AddScoped<ListLoad>();
		services.AddScoped<DetailsFill>();
		services.AddScoped<BurstPrimers>();
		services.AddScoped<IIdentityConfirmation, IdentityConfirmation>();
		services.AddScoped<IHousekeepingStep, DuplicateTitles>();
		services.AddScoped<IConflictReview, ConflictReview>();
		services.AddTracedScoped<IMediaService, MediaService>();
		services.AddTracedScoped<IMediaSearchService, MediaSearchService>();
	}
}
