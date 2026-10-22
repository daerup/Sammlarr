using Shared.DI;
using UI.Features.Feeds.Components.Runs;

namespace UI.Features.Feeds;

internal sealed class FeedsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<FeedRunProgress>();
	}
}
