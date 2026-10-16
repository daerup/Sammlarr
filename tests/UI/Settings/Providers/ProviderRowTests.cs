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

	public static IEnumerable<Func<(Service Key, string Id)>> Keys() =>
		[() => (TmdbRow, "provider-row-tmdb"), () => (AccountsRow, "provider-row-sammlarr"), () => (ArrRow, "provider-row-arr")];

	[Test]
	[MethodDataSource(nameof(Keys))]
	public async Task Row_ForEachKey_IsTheMapsScrollTargetUnderItsOwnId(Service key, string id)
	{
		var cut = Render<ProviderRow>(parameters =>
			parameters
				.Add(row => row.Key, key)
				.Add(row => row.Brand, Brands.GetBrandingInfo(MediaProvider.Tmdb))
				.Add(row => row.Status, "<span>Connected</span>")
				.AddChildContent("<p>The form</p>")
		);

		await Assert.That(cut.Find(".provider-row").Id).IsEqualTo(id);
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
