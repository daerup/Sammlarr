using Shared.DI;
using UI.Features.Feeds.Components;

namespace UI.Features.Feeds;

internal sealed class FeedsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<FeedRunProgress>();
	}
}
