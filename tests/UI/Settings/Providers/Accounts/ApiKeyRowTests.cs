using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Settings.Components.Providers.Accounts;
using UI.Shared.Common;

namespace UI.Tests.Settings.Providers.Accounts;

public class ApiKeyRowTests : BunitContext
{
	private const string Rejection = "The TMDB API key is not valid.";

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
	public async Task Status_WithNoSavedKey_ReadsNotSet()
	{
		var cut = RenderRow();

		await Assert.That(Status(cut)).IsEqualTo("Not set");
	}

	[Test]
	public async Task Status_WhileTheSavedKeyIsStillLoading_ShowsASkeletonPill()
	{
		_keys.GetAsync(Any<ApiKeyProvider>(), Any()).ReturnsAsync(new ValueTask<Result<IApiKey>>(new TaskCompletionSource<Result<IApiKey>>().Task));

		var cut = Render<ApiKeyRow>(parameters => parameters.Add(row => row.Provider, ApiKeyProvider.Tmdb));

		await Assert.That(cut.FindAll(@"button .bb\:animate-pulse")).HasSingleItem();
		await Assert.That(cut.Markup).DoesNotContain("Not set");
	}

	[Test]
	public async Task Status_AfterAKeyIsTyped_ReadsNotTested()
	{
		var cut = RenderRow();

		await TypeAsync(cut, "a-new-key");

		await Assert.That(Status(cut)).IsEqualTo("Not tested");
	}

	[Test]
	[Arguments("", "Required")]
	[Arguments("a-new-key", "Not tested")]
	public async Task Status_ForARequiredKey_ReadsRequiredUntilOneIsTyped(string typed, string status)
	{
		var cut = RenderRow(required: true);

		await TypeAsync(cut, typed);

		await Assert.That(Status(cut)).IsEqualTo(status);
	}

	[Test]
	public async Task Test_WhenTheProviderRejectsTheKey_ShowsInvalidAndLeavesTheReasonToTheBadge()
	{
		_keys.TestAsync(Any<ApiKeyProvider>(), Any<string>(), Any()).Returns(Error.From(Rejection));
		var cut = RenderRow();
		await TypeAsync(cut, "a-wrong-key");

		await Button(cut, "Test").ClickAsync(new MouseEventArgs());

		await Assert.That(Status(cut)).IsEqualTo("Invalid");
		await Assert.That(cut.Markup).DoesNotContain(Rejection);
	}

	[Test]
	public async Task Test_WhenTheProviderAcceptsTheKey_ReadsVerifiedAndOffersSaveWithNoSubtext()
	{
		_keys.TestAsync(Any<ApiKeyProvider>(), Any<string>(), Any()).Returns(Success.Of(IApiKey.Mock().Object));
		var cut = RenderRow();
		await TypeAsync(cut, "a-good-key");

		await Button(cut, "Test").ClickAsync(new MouseEventArgs());

		await Assert.That(Status(cut)).IsEqualTo("Verified");
		await Assert.That(Button(cut, "Save")).IsNotNull();
		await Assert.That(cut.Markup).DoesNotContain("accepted the key");
	}

	[Test]
	public async Task Layout_PutsTheButtonsOnTheFieldsLineWithTheDescriptionUnderIt()
	{
		var cut = RenderRow();

		var line = cut.Find("[data-key-line]");
		await Assert.That(line.QuerySelector("input")).IsNotNull();
		await Assert.That(line.QuerySelectorAll("button").Select(button => button.TextContent.Trim())).Contains("Test");
		await Assert.That(line.NextElementSibling!.TextContent).Contains("Your TMDB API key.");
	}

	[Test]
	public async Task Layout_ForASavedKey_PutsDeleteKeyOnTheFieldsLine()
	{
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Tmdb, "saved-key")));

		var cut = RenderRow();

		await Assert.That(cut.Find("[data-key-line]").TextContent).Contains("Delete key");
	}

	[Test]
	public async Task Layout_PutsTheSetupStepsBetweenTheFieldAndWhatItGetsYou()
	{
		var cut = Render<ApiKeyRow>(parameters =>
			parameters
				.Add(row => row.Provider, ApiKeyProvider.Mal)
				.Add(row => row.Description, "<span>Your MyAnimeList client ID.</span>")
				.Add(row => row.Uses, ["MyAnimeList anime lists as feeds"])
				.Add(row => row.Setup, "<div data-setup>How to get a client ID</div>")
		);

		var markup = cut.Markup;
		await Assert.That(markup.IndexOf("data-key-line", StringComparison.Ordinal)).IsLessThan(markup.IndexOf("data-setup", StringComparison.Ordinal));
		await Assert.That(markup.IndexOf("data-setup", StringComparison.Ordinal)).IsLessThan(markup.IndexOf("data-uses", StringComparison.Ordinal));
	}

	[Test]
	public async Task Field_ForAClientId_NamesItInItsLabelAndPlaceholder()
	{
		var cut = Render<ApiKeyRow>(parameters =>
			parameters
				.Add(row => row.Provider, ApiKeyProvider.Trakt)
				.Add(row => row.Noun, "client ID")
				.Add(row => row.Description, "<span>Your Trakt client ID.</span>")
		);

		await Assert.That(cut.Find("label").TextContent.Trim()).IsEqualTo("Client ID");
		await Assert.That(cut.Find("input").GetAttribute("placeholder")).IsEqualTo("Enter client ID");
	}

	[Test]
	public async Task Field_ByDefault_NamesAnApiKey()
	{
		var cut = RenderRow();

		await Assert.That(cut.Find("label").TextContent.Trim()).IsEqualTo("API key");
		await Assert.That(cut.Find("input").GetAttribute("placeholder")).IsEqualTo("Enter API key");
	}

	private static string Status(IRenderedComponent<ApiKeyRow> cut) => cut.Find("button").Children.Last().TextContent.Trim();

	private static IElement Button(IRenderedComponent<ApiKeyRow> cut, string text) => cut.FindAll("button").Single(button => button.TextContent.Trim() == text);

	private static Task TypeAsync(IRenderedComponent<ApiKeyRow> cut, string key) =>
		cut.InvokeAsync(() => cut.FindComponent<SensitiveInput>().Instance.ValueChanged.InvokeAsync(key));

	private IRenderedComponent<ApiKeyRow> RenderRow(bool required = false)
	{
		var cut = Render<ApiKeyRow>(parameters =>
			parameters.Add(row => row.Provider, ApiKeyProvider.Tmdb).Add(row => row.Description, "<span>Your TMDB API key.</span>").Add(row => row.Required, required)
		);
		cut.WaitForState(() => Status(cut).Length > 0);
		return cut;
	}
}
