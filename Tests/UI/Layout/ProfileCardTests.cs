using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Settings;
using Domain.Providers;
using Domain.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Layout;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Layout;

public class ProfileCardTests : BunitContext
{
	private static readonly ProviderConfig OnArr = new()
	{
		Sink = SinkKind.Arr,
		TrendingProvider = TrendingProvider.Tmdb,
		SearchProvider = SearchProvider.Plex,
		AuthProvider = AuthProvider.Plex,
		RegistrationMode = RegistrationMode.Open,
	};

	private static readonly MediaProviderBrandingService Brands = new();

	private readonly ProfileDropdownService _dropdown = new();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(_dropdown);
		JSInterop.Mode = JSRuntimeMode.Loose;
		AddAuthorization().SetAuthorized("Guest");
	}

	[Test]
	public async Task Card_WhenTheWantlistSyncsToRadarrAndSonarr_WearsTheSignInProvidersBrand()
	{
		var cut = await RenderOpenCardAsync(OnArr);

		await Assert.That(cut.WaitForElement("[aria-label='Open Plex account in new tab']")).IsNotNull();
		await Assert.That(cut.Find("[style*='linear-gradient']").GetAttribute("style")).Contains($"{Brands.GetBrandingInfo(MediaProvider.Plex).BrandColor}1F");
	}

	[Test]
	public async Task Card_OnASammlarrAccountsInstall_WearsSammlarrsBrand()
	{
		var cut = await RenderOpenCardAsync(OnArr with { Sink = SinkKind.Plex, AuthProvider = AuthProvider.Local });

		await Assert
			.That(cut.WaitForElement("[style*='linear-gradient']").GetAttribute("style"))
			.Contains($"{Brands.GetBrandingInfo(AuthProvider.Local).BrandColor}1F");
	}

	private async Task<IRenderedComponent<Harness>> RenderOpenCardAsync(ProviderConfig providers)
	{
		var config = IProviderConfigService.Mock();
		config.GetAsync(Any()).Returns(providers);
		Services.AddSingleton(config.Object);
		var cut = Render<Harness>();
		await cut.InvokeAsync(_dropdown.RequestOpenAsync);
		return cut;
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<ProfileCard>(1);
			builder.CloseComponent();
		}
	}
}
