using Contracts.DI;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.DI;
using TestInfrastructure;
using UI.Shared.Media;
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

		internal void AddMediaTitleLink()
		{
			var providers = IProviderConfigService.Mock();
			providers.GetAsync(Any()).Returns(OnPlex);
			services.TryAddSingleton<ICurrentUser>(new TestCurrentUser { Role = UserRole.Member });
			services.TryAddSingleton(providers.Object);
			services.TryAddSingleton(IArrSinkConfigService.Mock().Object);
			services.TryAddScoped<ExternalIdsView>();
		}
	}

	internal static readonly ProviderConfig OnPlex = new()
	{
		Sink = SinkKind.Plex,
		TrendingProvider = TrendingProvider.Tmdb,
		SearchProvider = SearchProvider.Tmdb,
		AuthProvider = AuthProvider.Plex,
		RegistrationMode = RegistrationMode.Open,
	};

	internal static readonly IKeyedServiceIndex<MediaProvider, IExternalPageLinker> Linkers = PageLinkers();

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
