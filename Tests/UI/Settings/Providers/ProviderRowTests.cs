using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings.Providers;

public class ProviderRowTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
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
	[Arguments(MediaProvider.Tmdb, true)]
	[Arguments(MediaProvider.Plex, false)]
	public async Task Row_WhenItsProviderIsRinged_DrawsTheRing(MediaProvider ringed, bool drawn)
	{
		var cut = Render<CascadingValue<MediaProvider?>>(parameters =>
			parameters
				.Add(value => value.Value, ringed)
				.Add(value => value.Name, ProviderRows.Ringed)
				.AddChildContent<ProviderRow>(row =>
					row.Add(provider => provider.Provider, MediaProvider.Tmdb)
						.Add(provider => provider.Status, "<span>Connected</span>")
						.AddChildContent("<p>The form</p>")
				)
		);

		await Assert.That(cut.Find(".provider-row").ClassList.Contains("is-ringed")).IsEqualTo(drawn);
		await Assert.That(cut.Find(".provider-row").Id).IsEqualTo("provider-row-tmdb");
	}

	private IRenderedComponent<ProviderRow> RenderRow(EventCallback<bool> openChanged = default) =>
		Render<ProviderRow>(parameters =>
			parameters
				.Add(row => row.Provider, MediaProvider.Tmdb)
				.Add(row => row.OpenChanged, openChanged)
				.Add(row => row.Status, "<span>Connected</span>")
				.AddChildContent("<p>The form</p>")
		);
}
