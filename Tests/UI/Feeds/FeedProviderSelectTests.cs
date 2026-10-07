using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class FeedProviderSelectTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Trigger_WithAProvider_TintsItWithThatBrand()
	{
		var cut = RenderSelect(FeedProvider.Mdblist);

		var trigger = cut.Find("[role=combobox]");
		await Assert.That(trigger.TextContent).Contains("MDBList");
		await Assert.That(trigger.GetAttribute("style")).IsEqualTo("--brand: #4284CA");
	}

	[Test]
	public async Task Trigger_WithoutAProvider_StaysNeutral()
	{
		var cut = RenderSelect(null, FeedProvider.Mdblist, FeedProvider.Tmdb, FeedProvider.AniList, FeedProvider.Mal, FeedProvider.Trakt);

		var trigger = cut.Find("[role=combobox]");
		await Assert.That(trigger.TextContent).Contains("No provider available");
		await Assert.That(trigger.HasAttribute("style")).IsFalse();
	}

	[Test]
	public async Task Trigger_WithNothingChosenYet_AsksForAProvider()
	{
		var cut = RenderSelect(null);

		var trigger = cut.Find("[role=combobox]");
		await Assert.That(trigger.TextContent).Contains("Select a provider");
		await Assert.That(trigger.HasAttribute("style")).IsFalse();
	}

	[Test]
	public async Task Trigger_WhenTheChosenProviderLostItsKey_KeepsItButGreysItOutLikeItsOption()
	{
		var cut = RenderSelect(FeedProvider.Mdblist, FeedProvider.Mdblist);

		var trigger = cut.Find("[role=combobox]");
		await Assert.That(trigger.TextContent).Contains("MDBList").And.Contains("No API key");
		await Assert.That(trigger.QuerySelector(".grayscale")).IsNotNull();
		await Assert.That(trigger.HasAttribute("style")).IsFalse();
		await Assert.That(trigger.HasAttribute("aria-invalid")).IsFalse();
	}

	[Test]
	public async Task Open_WithASelection_TintsEachRowWithItsBrand()
	{
		var cut = await OpenAsync(FeedProvider.Mdblist);

		var washes = cut.FindAll("[role=option] .provider-wash").Select(wash => wash.GetAttribute("style") ?? string.Empty).ToList();
		await Assert.That(washes).IsEquivalentTo(["--brand: #4284CA", "--brand: #01B4E4", "--brand: #02A9FF", "--brand: #2F52A2", "--brand: #9F42C6"]);
	}

	[Test]
	public async Task Open_ForAProviderWithoutAKey_DisablesItAndOffersItsKey()
	{
		var cut = await OpenAsync(null, FeedProvider.Mdblist);

		var mdblist = cut.FindAll("[role=option]").Single(option => option.TextContent.Contains("MDBList"));
		await Assert.That(mdblist.GetAttribute("data-disabled")).IsEqualTo("true");
		await Assert.That(mdblist.TextContent).Contains("No API key");
		await Assert.That(cut.Find("[role=listbox] a").GetAttribute("href")).IsEqualTo("/settings?section=providers&provider=mdblist");
	}

	[Test]
	public async Task Open_WithEveryKeySet_OffersNoKeyLink()
	{
		var cut = await OpenAsync(FeedProvider.Mdblist);

		await Assert.That(cut.Find("[role=listbox]").TextContent).DoesNotContain("No API key");
		await Assert.That(cut.FindAll("[role=listbox] a")).IsEmpty();
	}

	private async Task<IRenderedComponent<IComponent>> OpenAsync(FeedProvider? value, params FeedProvider[] unavailable)
	{
		var cut = RenderSelect(value, unavailable);
		await cut.Find("[role=combobox]").ClickAsync(new());
		return cut;
	}

	private IRenderedComponent<IComponent> RenderSelect(FeedProvider? value, params FeedProvider[] unavailable) =>
		Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedProviderSelect>(1);
			builder.AddComponentParameter(2, nameof(FeedProviderSelect.Value), value);
			builder.AddComponentParameter(3, nameof(FeedProviderSelect.Unavailable), unavailable);
			builder.CloseComponent();
		});
}
