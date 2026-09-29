using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Feeds.Components;
using UI.Shared.Services.Branding;

namespace UI.Tests.Feeds;

public class FeedProviderSelectTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(FeedProvider.Tmdb, "TMDb", "#01B4E4")]
	[Arguments(FeedProvider.Mdblist, "MDBList", "#4284CA")]
	public async Task Trigger_WithAProvider_TintsItWithThatBrand(FeedProvider provider, string nickName, string brandColor)
	{
		var cut = RenderSelect(provider);

		var trigger = cut.Find("[role=combobox]");
		await Assert.That(trigger.TextContent).Contains(nickName);
		await Assert.That(trigger.GetAttribute("style")).IsEqualTo($"--brand: {brandColor}");
	}

	[Test]
	public async Task Trigger_WithoutAProvider_StaysNeutral()
	{
		var cut = RenderSelect(null, FeedProvider.Tmdb, FeedProvider.Mdblist);

		var trigger = cut.Find("[role=combobox]");
		await Assert.That(trigger.TextContent).Contains("No provider available");
		await Assert.That(trigger.HasAttribute("style")).IsFalse();
	}

	[Test]
	public async Task Open_WithASelection_TintsEveryRowWithItsOwnBrand()
	{
		var cut = await OpenAsync(FeedProvider.Mdblist);

		var washes = cut.FindAll("[role=option] .provider-wash").Select(wash => wash.GetAttribute("style") ?? string.Empty).ToList();
		await Assert.That(washes).IsEquivalentTo(["--brand: #01B4E4", "--brand: #4284CA"]);
	}

	[Test]
	public async Task Open_ForAProviderWithoutAKey_DisablesItAndOffersTheKeys()
	{
		var cut = await OpenAsync(FeedProvider.Mdblist, FeedProvider.Tmdb);

		var tmdb = cut.FindAll("[role=option]").Single(option => option.TextContent.Contains("TMDb"));
		await Assert.That(tmdb.GetAttribute("data-disabled")).IsEqualTo("true");
		await Assert.That(tmdb.TextContent).Contains("No API key");
		await Assert.That(cut.Find("[role=listbox] a").GetAttribute("href")).IsEqualTo("/settings?section=providers");
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
