using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Ratings;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist.Arr;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Providers;
using UI.Features.Settings.Components.Ratings;
using UI.Shared.Services;
using UI.Tests.Media;
using UI.Tests.Providers;
using SettingsPage = UI.Features.Settings.Pages.Settings;

namespace UI.Tests.Settings;

public class SettingsPageTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(RatingStub.Context());
		var ratings = IRatingService.Mock();
		ratings.GetImdbFileDateAsync(Any()).Returns(new None());
		Services.AddSingleton(ratings.Object);
		Services.AddProviderBranding();
		Services.AddSingleton<ProfileDropdownService>();
		Services.AddSingleton<AccountConnectionPrompt>();
		Services.AddScoped<Notifier>();
		Services.AddScoped<ScrollService>();
		Services.AddSingleton(ISinkSwitch.Mock().Object);
		Services.AddSingleton(IUserManagementService.Mock().Object);
		var arrConfigs = IArrSinkConfigService.Mock();
		arrConfigs.GetAsync(Any()).Returns(ArrSinkConfig.Empty);
		Services.AddSingleton(arrConfigs.Object);
		AddAuthorization().SetAuthorized("alice");

		var config = IProviderConfigService.Mock();
		config
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(config.Object);

		var keys = IApiKeyService.Mock();
		keys.GetAsync(Any<ApiKeyProvider>(), Any()).Returns(Error.From("No API key is configured for this provider."));
		Services.AddSingleton(keys.Object);

		var credentials = ICredentialService.Mock();
		credentials.GetAsync(Any<AuthProvider>(), Any()).Returns(Error.From("No credential is stored for this provider."));
		Services.AddSingleton(credentials.Object);
		Services.AddSingleton(ReadinessStub.Connected(new HashSet<RemoteService>()));

		Services.AddSingleton(ICurrentUser.Mock().Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments("tmdb", RemoteService.Tmdb)]
	[Arguments("mdblist", RemoteService.Mdblist)]
	[Arguments("plex", RemoteService.Plex)]
	public async Task Providers_WhenLinkedToAProvider_StartsClosedThenOpensOnlyThatProvidersRow(string slug, RemoteService remote)
	{
		var cut = RenderAt($"/settings?section=providers&provider={slug}");

		await Assert.That(OpenRows(cut)).IsEmpty();
		cut.WaitForState(() => OpenRows(cut).Any());
		await Assert.That(OpenRows(cut)).IsEquivalentTo([(Service)remote]);
	}

	[Test]
	[Arguments("/settings?section=providers")]
	[Arguments("/settings?section=providers&provider=not-a-provider")]
	public async Task Providers_WithoutAKnownProvider_OpensNoRow(string uri)
	{
		var cut = RenderAt(uri);

		await Assert.That(OpenRows(cut)).IsEmpty();
	}

	[Test]
	public async Task SectionNav_AfterALinkToAProvider_DropsTheProvider()
	{
		var cut = RenderAt("/settings?section=providers&provider=tmdb");

		await cut.FindAll("nav button").Single(button => button.TextContent.Trim() == "Providers").ClickAsync(new());

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/settings?section=providers");
	}

	[Test]
	public async Task SectionNav_ForAnAdmin_OffersTheTestingPlaygroundOnlyInDebugBuilds()
	{
		var admin = ICurrentUser.Mock();
		admin.Role.Getter.Returns(UserRole.Admin);
		Services.AddSingleton(admin.Object);

		var cut = RenderAt("/settings?section=providers");

		var offered = cut.FindAll("nav button").Select(button => button.TextContent.Trim());
#if DEBUG
		await Assert.That(offered).Contains("Testing");
#else
		await Assert.That(offered).DoesNotContain("Testing");
#endif
	}

	[Test]
	public async Task SectionNav_ForAMember_OffersRatingsWhoseLinkOpensIt()
	{
		var member = ICurrentUser.Mock();
		member.Role.Getter.Returns(UserRole.Member);
		Services.AddSingleton(member.Object);

		var cut = RenderAt(SettingsLinks.Ratings);

		await Assert.That(cut.FindAll("nav button").Select(button => button.TextContent.Trim())).Contains("Ratings");
		cut.WaitForState(() => cut.FindComponents<RatingPipelineCard>().Count > 0);
	}

	[Test]
	public async Task SectionNav_WhenTheBrowserGoesBackToSettingsWithoutASection_ShowsTheFirstSectionAgain()
	{
		var cut = RenderAt(SettingsLinks.Ratings);
		cut.WaitForState(() => cut.FindComponents<RatingPipelineCard>().Count > 0);

		Services.GetRequiredService<NavigationManager>().NavigateTo("/settings");
		cut.Render();

		await Assert.That(cut.FindComponents<ProvidersSection>()).HasSingleItem();
	}

	[Test]
	public async Task SectionNav_ForAMember_OffersTheirAccountButNotTheUsers()
	{
		var member = ICurrentUser.Mock();
		member.Role.Getter.Returns(UserRole.Member);
		Services.AddSingleton(member.Object);

		var cut = RenderAt("/settings?section=providers");

		var offered = cut.FindAll("nav button").Select(button => button.TextContent.Trim()).ToList();
		await Assert.That(offered).Contains("Account");
		await Assert.That(offered).DoesNotContain("Users");
	}

	private static IEnumerable<Service> OpenRows(IRenderedComponent<SettingsPage> cut) =>
		cut.FindComponents<ProviderRow>().Where(row => row.Instance.Open).Select(row => row.Instance.Key);

	private IRenderedComponent<SettingsPage> RenderAt(string uri)
	{
		Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
		return Render<SettingsPage>();
	}
}
