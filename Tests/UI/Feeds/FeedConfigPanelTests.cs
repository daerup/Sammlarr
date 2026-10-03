using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Auth;
using Domain.Feed;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Editor;
using UI.Shared.Services.Branding;

namespace UI.Tests.Feeds;

public class FeedConfigPanelTests : BunitContext
{
	private readonly List<FeedConfig> _changes = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of(IApiKey.Mock().Object));
		keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Error.From("No API key is configured for this provider."));
		Services.AddSingleton(keys.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Provider_WhenAnOpenedFeedsProviderHasNoKey_StaysChosenSaysWhereToAddItAndBlocksTesting()
	{
		var cut = RenderPanel(FeedProvider.Tmdb);

		var config = _changes.Last();
		await Assert.That(config.Provider).IsEqualTo(FeedProvider.Tmdb);
		await Assert.That(config.IsTestable).IsFalse();
		await Assert.That(ProviderLabel(cut).TextContent).Contains("*");
		await Assert.That(cut.Markup).Contains("API key is missing, fix in");
		await Assert
			.That(cut.FindAll("a").Single(link => link.TextContent.Trim() == "Settings").GetAttribute("href"))
			.IsEqualTo("/settings?section=providers&provider=tmdb");
	}

	[Test]
	public async Task Provider_OnANewFeed_StartsEmptyAndAsksForOne()
	{
		var cut = RenderPanel(null);

		await Assert.That(cut.Find("[role=combobox]").TextContent).Contains("Select a provider");
		await Assert.That(_changes).IsEmpty();
		await Assert.That(cut.Markup).DoesNotContain("API key is missing");
	}

	[Test]
	public async Task Strategy_OnAppend_ShowsNoRunCount()
	{
		var cut = RenderPanel(FeedProvider.Mdblist);

		await Assert.That(cut.Markup).DoesNotContain("Removes an item after");
	}

	[Test]
	public async Task Strategy_WhenMirrorIsPickedOnANewFeed_AsksForThreeMissedRuns()
	{
		var cut = RenderPanel(FeedProvider.Mdblist);

		await StrategyRadio(cut, "Mirror").ClickAsync();

		await Assert.That(RunLine(cut).TextContent).Contains("Removes an item after").And.Contains("missed runs.");
		await Assert.That(cut.Find("input[aria-label='Runs missing before removal']").GetAttribute("value")).IsEqualTo("3");
		var mirror = await Assert.That(_changes.Last().Strategy).IsOfUnionType<FeedStrategy, Mirror>();
		await Assert.That(mirror.AbsenceThreshold).IsEqualTo(3);
	}

	[Test]
	public async Task Strategy_WhenTheSavedThresholdIsOne_ReadsSingularInAmber()
	{
		var cut = RenderPanel(FeedProvider.Mdblist, new Mirror(1));

		await Assert.That(RunLine(cut).ClassList.Contains("text-amber-500")).IsTrue();
		await Assert.That(RunLine(cut).TextContent).Contains("missed run.");
	}

	[Test]
	public async Task Strategy_WhenTheRunCountSteps_RaisesMirrorWithTheNewCount()
	{
		var cut = RenderPanel(FeedProvider.Mdblist, new Mirror(3));

		await cut.Find(".run-count-line button[aria-label='Increase value']").ClickAsync();

		var mirror = await Assert.That(_changes.Last().Strategy).IsOfUnionType<FeedStrategy, Mirror>();
		await Assert.That(mirror.AbsenceThreshold).IsEqualTo(4);
	}

	[Test]
	public async Task Strategy_WhenTheRunCountIsClearedOrZero_KeepsMirrorAtOne()
	{
		var cut = RenderPanel(FeedProvider.Mdblist, new Mirror(3));
		var runCount = cut.FindComponent<BbNumericInput<int>>();

		await cut.InvokeAsync(() => runCount.Instance.JsOnBlur("0"));

		var mirror = await Assert.That(_changes.Last().Strategy).IsOfUnionType<FeedStrategy, Mirror>();
		await Assert.That(mirror.AbsenceThreshold).IsEqualTo(1);
	}

	private static IElement ProviderLabel(IRenderedComponent<FeedConfigPanel> cut)
	{
		return cut.FindAll("label").First(label => label.TextContent.Trim().StartsWith("Provider"));
	}

	private static IElement StrategyRadio(IRenderedComponent<FeedConfigPanel> cut, string name)
	{
		return cut.FindAll("[role=radio]").First(radio => radio.TextContent.Trim() == name);
	}

	private static IElement RunLine(IRenderedComponent<FeedConfigPanel> cut) => cut.Find(".run-count-line");

	private IRenderedComponent<FeedConfigPanel> RenderPanel(FeedProvider? provider) => RenderPanel(provider, new Append());

	private IRenderedComponent<FeedConfigPanel> RenderPanel(FeedProvider? provider, FeedStrategy strategy)
	{
		return Render<FeedConfigPanel>(parameters =>
			parameters
				.Add(
					panel => panel.Value,
					new FeedConfig
					{
						Name = "Documentaries",
						Provider = provider,
						Identifier = "hdlists/documentaries",
						Strategy = strategy,
					}
				)
				.Add(panel => panel.OnChange, EventCallback.Factory.Create<FeedConfig>(this, _changes.Add))
		);
	}
}
