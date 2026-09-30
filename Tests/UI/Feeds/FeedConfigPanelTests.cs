using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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
		keys.GetFeedProvidersWithoutKeyAsync(Any()).Returns(new HashSet<FeedProvider> { FeedProvider.Tmdb });
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

	private static IElement ProviderLabel(IRenderedComponent<FeedConfigPanel> cut) =>
		cut.FindAll("label").First(label => label.TextContent.Trim().StartsWith("Provider"));

	private IRenderedComponent<FeedConfigPanel> RenderPanel(FeedProvider? provider) =>
		Render<FeedConfigPanel>(parameters =>
			parameters
				.Add(
					panel => panel.Value,
					new FeedConfig
					{
						Name = "Documentaries",
						Provider = provider,
						Identifier = "hdlists/documentaries",
					}
				)
				.Add(panel => panel.OnChange, EventCallback.Factory.Create<FeedConfig>(this, _changes.Add))
		);
}
