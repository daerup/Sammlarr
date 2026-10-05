using Contracts.DI;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using TestInfrastructure;
using UI.Shared.Services.Branding;
using UI.Shared.Services.Links;

namespace UI.Tests;

internal static class ProviderBranding
{
	extension(IServiceCollection services)
	{
		internal void AddProviderBranding()
		{
			services.AddSingleton<MediaProviderBrandingService>();
			services.AddSingleton(Linkers);
		}
	}

	private static readonly IKeyedServiceIndex<MediaProvider, IExternalPageLinker> Linkers = PageLinkers();

	private static IKeyedServiceIndex<MediaProvider, IExternalPageLinker> PageLinkers()
	{
		var registered = new ServiceCollection();
		registered.AddModule<LinksModule>();
		var linkers = registered.BuildServiceProvider();
		return Enum.GetValues<MediaProvider>()
			.Aggregate(
				new FakeKeyedServiceIndex<MediaProvider, IExternalPageLinker>(),
				(index, provider) => index.Register(provider, linkers.GetRequiredKeyedService<IExternalPageLinker>(provider))
			);
	}
}
