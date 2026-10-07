using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class OptionalKeyTests : BunitContext
{
	private readonly Mock<IApiKeyService> _keys = IApiKeyService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		_keys.GetAsync(Any<ApiKeyProvider>(), Any()).Returns(Error.From("No API key is configured for this provider."));
		Services.AddSingleton(_keys.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WithNoToken_StartsClosedSayingItIsOptionalAndNotSet()
	{
		var cut = RenderLine();

		var trigger = Trigger(cut);
		await Assert.That(trigger.GetAttribute("aria-expanded")).IsEqualTo("false");
		await Assert.That(trigger.TextContent).Contains("Read access token").And.Contains("optional").And.Contains("Not set");
	}

	[Test]
	public async Task Render_WithASavedToken_StaysClosedAndSaysItIsConnected()
	{
		_keys.GetAsync(ApiKeyProvider.TmdbReadToken, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.TmdbReadToken, "saved-token")));

		var cut = RenderLine();

		await Assert.That(Trigger(cut).GetAttribute("aria-expanded")).IsEqualTo("false");
		await Assert.That(Trigger(cut).TextContent).Contains("Connected");
	}

	[Test]
	public async Task Field_KeepsItsLabelForScreenReadersOnly()
	{
		var cut = RenderLine();

		var label = cut.FindAll("label").Single(element => element.TextContent.Trim() == "Read access token");
		await Assert.That(label.ClassList).Contains("sr-only");
	}

	[Test]
	public async Task Trigger_OpensTheTokenField()
	{
		var cut = RenderLine();

		await Trigger(cut).ClickAsync(new());

		await Assert.That(Trigger(cut).GetAttribute("aria-expanded")).IsEqualTo("true");
		await Assert.That(cut.Find("[data-slot=tmdbreadtoken] input").GetAttribute("placeholder")).IsEqualTo("Enter read access token");
	}

	private static IElement Trigger(IRenderedComponent<OptionalKey> cut) => cut.Find("[data-optional-key] button");

	private IRenderedComponent<OptionalKey> RenderLine()
	{
		var cut = Render<OptionalKey>(parameters =>
			parameters
				.Add(line => line.Provider, ApiKeyProvider.TmdbReadToken)
				.Add(line => line.Title, "Read access token")
				.Add(line => line.Hint, "for lists only TMDB's v4 API serves")
				.Add(line => line.Noun, "read access token")
				.Add(line => line.DeleteText, "Delete token")
				.Add(line => line.Uses, "lists only TMDB's v4 API serves")
				.Add(line => line.Description, "<span>The API Read Access Token.</span>")
		);
		cut.WaitForState(() => cut.FindAll("[data-optional-key] button .bb\\:animate-pulse").Count == 0);
		return cut;
	}
}
