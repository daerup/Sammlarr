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
	private readonly ProfileDropdownService _dropdown = new();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddSingleton(_dropdown);
		JSInterop.Mode = JSRuntimeMode.Loose;
		AddAuthorization().SetAuthorized("Guest");
	}

	[Test]
	public async Task Card_WhenTheWantlistSyncsToRadarrAndSonarr_WearsTheSignInProvidersBrand()
	{
		var config = IProviderConfigService.Mock();
		config
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Arr,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(config.Object);
		var plex = new MediaProviderBrandingService().GetBrandingInfo(MediaProvider.Plex).BrandColor;

		var cut = Render<Harness>();
		await cut.InvokeAsync(_dropdown.RequestOpenAsync);

		await Assert.That(cut.WaitForElement("[aria-label='Open Plex account in new tab']")).IsNotNull();
		await Assert.That(cut.Find("[style*='linear-gradient']").GetAttribute("style")).Contains($"{plex}1F");
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
