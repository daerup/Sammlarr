using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Providers;
using Domain.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Shared.Layout;
using UI.Shared.Services;

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

	private readonly ProfileDropdownService _dropdown = new();

	private readonly TestCurrentUser _user = new() { DisplayName = "Guest" };

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(_dropdown);
		Services.AddSingleton<ICurrentUser>(_user);
		var users = IUserManagementService.Mock();
		users.GetColourRosterAsync(Any()).Returns([new RosterColour(_user.Id, DateTimeOffset.UnixEpoch, StoredColour.Empty)]);
		Services.AddSingleton(users.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
		AddAuthorization().SetAuthorized("Guest");
	}

	[Test]
	public async Task Card_WhenTheWantlistSyncsToRadarrAndSonarr_LinksTheSignInProvidersAccount()
	{
		var cut = await RenderOpenCardAsync(OnArr);

		await Assert.That(cut.WaitForElement("[aria-label='Open Plex account in new tab']")).IsNotNull();
	}

	[Test]
	public async Task Avatar_ForAnAccountWithoutAPicture_DrawsTheirBird()
	{
		var cut = await RenderOpenCardAsync(OnArr with { AuthProvider = AuthProvider.Local });

		await Assert.That(cut.WaitForElement("#profile-dropdown-trigger [data-bird]")).IsNotNull();
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
