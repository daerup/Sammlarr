using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Providers;
using UI.Shared.Common;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings.Providers;

public class ApiKeyRowTests : BunitContext
{
	private const string Rejection = "The TMDB API key is not valid.";

	private readonly Mock<IApiKeyFactory> _factory = IApiKeyFactory.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		var keys = IApiKeyConfigRepository.Mock();
		keys.GetApiKeyAsync(Any<ApiKeyProvider>(), Any()).Returns(Error.From("No API key is configured for this provider."));
		Services.AddSingleton(keys.Object);
		Services.AddSingleton(_factory.Object);
		Services.AddSingleton(IApiKeyService.Mock().Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Status_WithNoSavedKey_ReadsNotSet()
	{
		var cut = RenderRow();

		await Assert.That(Status(cut)).IsEqualTo("Not set");
	}

	[Test]
	public async Task Status_AfterAKeyIsTyped_ReadsNotTested()
	{
		var cut = RenderRow();

		await TypeAsync(cut, "a-new-key");

		await Assert.That(Status(cut)).IsEqualTo("Not tested");
	}

	[Test]
	public async Task Test_WhenTheProviderRejectsTheKey_OutlinesTheFieldAndLeavesTheReasonToTheBadge()
	{
		_factory.CreateAsync(Any<ApiKeyProvider>(), Any<string>(), Any()).Returns(Error.From(Rejection));
		var cut = RenderRow();
		await TypeAsync(cut, "a-wrong-key");

		await Button(cut, "Test").ClickAsync(new MouseEventArgs());

		await Assert.That(Status(cut)).IsEqualTo("Invalid");
		await Assert.That(cut.Find("input").ClassList.Contains("border-destructive")).IsTrue();
		await Assert.That(cut.Markup).DoesNotContain(Rejection);
	}

	[Test]
	public async Task Test_WhenTheProviderAcceptsTheKey_ReadsVerifiedAndOffersSaveWithNoSubtext()
	{
		_factory.CreateAsync(Any<ApiKeyProvider>(), Any<string>(), Any()).Returns(Success.Of(IApiKey.Mock().Object));
		var cut = RenderRow();
		await TypeAsync(cut, "a-good-key");

		await Button(cut, "Test").ClickAsync(new MouseEventArgs());

		await Assert.That(Status(cut)).IsEqualTo("Verified");
		await Assert.That(Button(cut, "Save")).IsNotNull();
		await Assert.That(cut.Markup).DoesNotContain("accepted the key");
	}

	private static string Status(IRenderedComponent<ApiKeyRow> cut) => cut.Find("button").Children.Last().TextContent.Trim();

	private static IElement Button(IRenderedComponent<ApiKeyRow> cut, string text) => cut.FindAll("button").Single(button => button.TextContent.Trim() == text);

	private static Task TypeAsync(IRenderedComponent<ApiKeyRow> cut, string key) =>
		cut.InvokeAsync(() => cut.FindComponent<SensitiveInput>().Instance.ValueChanged.InvokeAsync(key));

	private IRenderedComponent<ApiKeyRow> RenderRow() =>
		Render<ApiKeyRow>(parameters => parameters.Add(row => row.Provider, ApiKeyProvider.Tmdb).Add(row => row.Description, "<span>Your TMDB API key.</span>"));
}
