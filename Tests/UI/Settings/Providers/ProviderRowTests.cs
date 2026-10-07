using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings.Providers;

public class ProviderRowTests : BunitContext
{
	private static readonly MediaProviderBrandingService Brands = new();

	private static readonly Service TmdbRow = RemoteService.Tmdb;

	private static readonly Service AccountsRow = new SammlarrAccounts();

	private static readonly Service ArrRow = new Arr();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Row_Always_PutsTheStatusLast()
	{
		var cut = RenderRow();

		var status = cut.Find("button").Children.Last();
		await Assert.That(status.TextContent.Trim()).IsEqualTo("Connected");
	}

	[Test]
	public async Task Row_Always_TitlesItselfWithTheProvidersNamedLogo()
	{
		var cut = RenderRow();

		var logos = cut.FindAll("button img").Select(logo => logo.GetAttribute("alt") ?? string.Empty);
		await Assert.That(logos).IsEquivalentTo(["The Movie Database"]);
	}

	[Test]
	public async Task Row_WhenClicked_AsksToOpenItsForm()
	{
		bool? requested = null;
		var cut = RenderRow(EventCallback.Factory.Create<bool>(this, open => requested = open));

		await cut.Find("button").ClickAsync(new MouseEventArgs());

		await Assert.That(requested).IsTrue();
	}

	[Test]
	public async Task Row_Always_ShowsTheMapsRolesInPlaceOfACaption()
	{
		var cut = RenderRow();

		var roles = cut.FindAll(".provider-role .sr-only").Select(role => role.TextContent.Trim());
		await Assert.That(roles).IsEquivalentTo(["Search", "Trending", "Feeds"], CollectionOrdering.Matching);
		await Assert.That(cut.Markup).DoesNotContain("API key ·");
	}

	[Test]
	public async Task Row_ForAProvider_IsTheMapsScrollTargetWithoutARing()
	{
		var cut = Render<ProviderRow>(row =>
			row.Add(provider => provider.Key, TmdbRow)
				.Add(provider => provider.Brand, Brands.GetBrandingInfo(MediaProvider.Tmdb))
				.Add(provider => provider.Status, "<span>Connected</span>")
				.AddChildContent("<p>The form</p>")
		);

		await Assert.That(cut.Find(".provider-row").Id).IsEqualTo("provider-row-tmdb");
		await Assert.That(cut.Find(".provider-row").ClassList.Contains("is-ringed")).IsFalse();
	}

	[Test]
	public async Task Row_ForSammlarrAccounts_HasItsOwnIdAndOnlyTheSignInRole()
	{
		var cut = Render<ProviderRow>(parameters =>
			parameters
				.Add(row => row.Key, AccountsRow)
				.Add(row => row.Brand, Brands.Brands(AuthProvider.Local.Service).Single())
				.Add(row => row.Status, "<span>Signed in</span>")
				.AddChildContent("<p>The form</p>")
		);

		await Assert.That(cut.Find(".provider-row").Id).IsEqualTo("provider-row-sammlarr");
		await Assert.That(cut.FindAll(".provider-role .sr-only").Select(role => role.TextContent)).IsEquivalentTo(["Sign-in"]);
	}

	[Test]
	public async Task Row_ForRadarrAndSonarr_HasItsOwnIdAndTheWantlistAndSearchRoles()
	{
		var cut = Render<ProviderRow>(parameters =>
			parameters
				.Add(row => row.Key, ArrRow)
				.Add(row => row.Brand, Brands.GetBrandingInfo(RemoteService.Radarr))
				.Add(row => row.Status, "<span>Connected</span>")
				.AddChildContent("<p>The form</p>")
		);

		await Assert.That(cut.Find(".provider-row").Id).IsEqualTo("provider-row-arr");
		await Assert.That(cut.FindAll(".provider-role .sr-only").Select(role => role.TextContent)).IsEquivalentTo(["Wantlist", "Search"]);
	}

	private IRenderedComponent<ProviderRow> RenderRow(EventCallback<bool> openChanged = default) =>
		Render<ProviderRow>(parameters =>
			parameters
				.Add(row => row.Key, TmdbRow)
				.Add(row => row.Brand, Brands.GetBrandingInfo(MediaProvider.Tmdb))
				.Add(row => row.OpenChanged, openChanged)
				.Add(row => row.Status, "<span>Connected</span>")
				.AddChildContent("<p>The form</p>")
		);
}
