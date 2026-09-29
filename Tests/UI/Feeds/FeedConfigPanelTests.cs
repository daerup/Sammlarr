using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Auth;
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
		var keys = IApiKeyConfigRepository.Mock();
		keys.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).Returns(Error.From("No API key is configured for this provider."));
		keys.GetApiKeyAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of(IApiKey.Mock().Object));
		Services.AddSingleton(keys.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Provider_WhenAnOpenedFeedsProviderHasNoKey_StaysChosenSaysWhereToAddItAndBlocksTesting()
	{
		var cut = RenderPanel(FeedProvider.Tmdb, isNew: false);

		var config = _changes.Last();
		await Assert.That(config.Provider).IsEqualTo(FeedProvider.Tmdb);
		await Assert.That(config.IsTestable).IsFalse();
		await Assert.That(ProviderLabel(cut).TextContent).Contains("*");
		await Assert.That(cut.Markup).Contains("Add the TMDb API key in");
	}

	[Test]
	public async Task Provider_WhenANewFeedStartsOnOneWithNoKey_MovesToOneThatHasAKey()
	{
		var cut = RenderPanel(FeedProvider.Tmdb, isNew: true);

		var config = _changes.Last();
		await Assert.That(config.Provider).IsEqualTo(FeedProvider.Mdblist);
		await Assert.That(config.IsTestable).IsTrue();
		await Assert.That(ProviderLabel(cut).TextContent).Contains("*");
		await Assert.That(cut.Markup).DoesNotContain("API key in");
	}

	private static IElement ProviderLabel(IRenderedComponent<FeedConfigPanel> cut) =>
		cut.FindAll("label").First(label => label.TextContent.Trim().StartsWith("Provider"));

	private IRenderedComponent<FeedConfigPanel> RenderPanel(FeedProvider provider, bool isNew) =>
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
				.Add(panel => panel.IsNew, isNew)
				.Add(panel => panel.OnChange, EventCallback.Factory.Create<FeedConfig>(this, _changes.Add))
		);
}
