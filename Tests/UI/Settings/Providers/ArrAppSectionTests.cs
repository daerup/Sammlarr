using System.Text.RegularExpressions;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Settings;
using Domain.Wantlist.Arr;
using TestInfrastructure;
using UI.Features.Settings.Components.Providers;
using UI.Shared.Common;

namespace UI.Tests.Settings.Providers;

public class ArrAppSectionTests : BunitContext
{
	private static readonly ArrInstanceConfig Stored = ArrInstanceConfig.Empty with
	{
		Url = Some.Of("http://sonarr:8989"),
		ApiKey = Some.Of("0123456789abcdef0123456789abcdef"),
		QualityProfileId = Some.Of(4),
		RootFolderPath = Some.Of("/tv"),
	};

	private static readonly ArrInstanceChoices Choices = new([new ArrQualityProfile(4, "HD-1080p")], [new ArrRootFolder("/tv", 1319413953331)]);

	private readonly List<ArrInstanceConfig> _tested = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Section_WhenTheTestFailed_SaysWhyAndOutlinesTheKey()
	{
		const string why = "Sonarr answered 401 Unauthorized at http://sonarr:8989. Check the API key under Settings › General in Sonarr.";

		var cut = RenderSection(Stored, ArrConnectionState.Failed, why);

		await Assert.That(cut.Find("[role=alert]").TextContent.Trim()).IsEqualTo(why);
		await Assert.That(cut.FindComponent<SensitiveInput>().Instance.Class).Contains("border-destructive");
		await Assert.That(cut.Markup).DoesNotContain("Quality profile");
	}

	[Test]
	public async Task Section_BeforeATest_OffersNoProfilesOrFolders()
	{
		var cut = RenderSection(Stored, ArrConnectionState.Untested);

		await Assert.That(cut.Markup).Contains("Test the connection to load Sonarr's quality profiles and root folders.");
		await Assert.That(cut.Markup).DoesNotContain("Root folder");
	}

	[Test]
	public async Task Section_OnceConnected_OffersTheAppsProfilesAndFoldersWithTheirFreeSpace()
	{
		var cut = RenderSection(Stored, ArrConnectionState.Connected);

		await Assert.That(cut.Markup).Contains("Quality profile");
		await Assert.That(cut.Markup).Contains("/tv · 1.2 TB free");
	}

	[Test]
	public async Task Section_WhenTheAppHasNoRootFolder_SaysWhereToAddOne()
	{
		var cut = Render<ArrAppSection>(parameters =>
			parameters
				.Add(section => section.App, ArrApp.Sonarr)
				.Add(section => section.Instance, Stored)
				.Add(section => section.State, ArrConnectionState.Connected)
				.Add(section => section.Choices, new ArrInstanceChoices([new ArrQualityProfile(4, "HD-1080p")], []))
		);

		await Assert
			.That(cut.Find("[role=alert]").TextContent.Trim())
			.IsEqualTo("Sonarr has no root folder yet. Add one under Settings › Media Management in Sonarr, then test again.");
	}

	[Test]
	public async Task Monitor_WhenSwitchedOff_RaisesTheChange()
	{
		List<ArrInstanceConfig> changes = [];
		var cut = RenderWithChanges(Stored, changes);

		await cut.Find("button[role=switch][aria-label='Sonarr monitors new titles']").ClickAsync();

		await Assert.That(changes.Single().Monitored).IsFalse();
	}

	[Test]
	public async Task Test_AfterANewAddressIsTyped_TestsWhatWasTyped()
	{
		var cut = RenderSection(Stored, ArrConnectionState.Connected);

		await cut.InvokeAsync(() => cut.FindComponent<BbInput>().Instance.ValueChanged.InvokeAsync("http://sonarr.lan:8989"));
		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Test again").ClickAsync();

		await Assert.That(_tested.Single().Url).IsSome().And.IsEqualTo("http://sonarr.lan:8989");
	}

	[Test]
	public async Task Key_WithoutAUrl_NamesWhereItIsFoundWithoutALink()
	{
		var cut = RenderSection(ArrInstanceConfig.Empty, ArrConnectionState.Empty);

		await Assert.That(Description(cut)).IsEqualTo("Found in Sonarr › Settings › General.");
		await Assert.That(cut.FindAll("a.app-link")).IsEmpty();
	}

	[Test]
	public async Task Key_OnceAUrlIsTyped_LinksToTheAppsGeneralSettingsInANewTab()
	{
		var cut = RenderSection(ArrInstanceConfig.Empty, ArrConnectionState.Empty);

		await cut.InvokeAsync(() => cut.FindComponent<BbInput>().Instance.ValueChanged.InvokeAsync("http://host/sonarr/"));

		var link = cut.Find("a.app-link");
		await Assert.That(link.GetAttribute("href")).IsEqualTo("http://host/sonarr/settings/general");
		await Assert.That(link.GetAttribute("target")).IsEqualTo("_blank");
		await Assert.That(link.TextContent.Trim()).IsEqualTo("Sonarr › Settings › General");
		await Assert.That(Description(cut)).IsEqualTo("Found in Sonarr › Settings › General.");
	}

	[Test]
	public async Task Key_WhenTheUrlIsClearedAgain_DropsTheLink()
	{
		var cut = RenderSection(ArrInstanceConfig.Empty, ArrConnectionState.Empty);
		var url = cut.FindComponent<BbInput>().Instance;
		await cut.InvokeAsync(() => url.ValueChanged.InvokeAsync("http://host/sonarr/"));

		await cut.InvokeAsync(() => url.ValueChanged.InvokeAsync(null));

		await Assert.That(cut.FindAll("a.app-link")).IsEmpty();
		await Assert.That(Description(cut)).IsEqualTo("Found in Sonarr › Settings › General.");
	}

	[Test]
	public async Task Key_WithAnExternalUrl_LinksThereInsteadOfToTheUrl()
	{
		var cut = RenderSection(Stored with { ExternalUrl = Some.Of("http://localhost:8990") }, ArrConnectionState.Connected);

		await Assert.That(cut.Find("a.app-link").GetAttribute("href")).IsEqualTo("http://localhost:8990/settings/general");
	}

	[Test]
	public async Task ExternalUrl_WhenTyped_RaisesTheChangeTrimmed()
	{
		List<ArrInstanceConfig> changes = [];
		var cut = RenderWithChanges(Stored, changes);

		await cut.InvokeAsync(() => ExternalUrlInput(cut).ValueChanged.InvokeAsync(" http://localhost:8990 "));

		await Assert.That(changes.Single()).IsEqualTo(Stored with { ExternalUrl = Some.Of("http://localhost:8990") });
	}

	[Test]
	public async Task ExternalUrl_WhenCleared_RaisesTheChangeWithoutOne()
	{
		List<ArrInstanceConfig> changes = [];
		var cut = RenderWithChanges(Stored with { ExternalUrl = Some.Of("http://localhost:8990") }, changes);

		await cut.InvokeAsync(() => ExternalUrlInput(cut).ValueChanged.InvokeAsync(null));

		await Assert.That(changes.Single()).IsEqualTo(Stored);
	}

	[Test]
	[Arguments(ArrApp.Radarr, "movies", "http://radarr:7878")]
	[Arguments(ArrApp.Sonarr, "shows", "http://sonarr:8989")]
	public async Task Section_ForAnApp_NamesWhatItAddsAndSuggestsItsDefaultAddress(ArrApp app, string adds, string address)
	{
		var cut = RenderSection(ArrInstanceConfig.Empty, ArrConnectionState.Empty, app: app);

		await Assert.That(cut.Find(".arr-app-for").TextContent).IsEqualTo(adds);
		await Assert.That(cut.Find($"input[aria-label='{app} URL']").GetAttribute("placeholder")).IsEqualTo(address);
	}

	private static string Description(IRenderedComponent<ArrAppSection> cut)
	{
		var description = cut.FindAll("*").Last(element => element.TextContent.Contains("Found in"));
		return Regex.Replace(description.TextContent, @"\s+", " ").Trim();
	}

	private static BbInput ExternalUrlInput(IRenderedComponent<ArrAppSection> cut)
	{
		return cut.FindComponents<BbInput>().Single(input => input.Instance.AriaLabel == "Sonarr external URL").Instance;
	}

	private IRenderedComponent<ArrAppSection> RenderWithChanges(ArrInstanceConfig instance, List<ArrInstanceConfig> changes)
	{
		return Render<ArrAppSection>(parameters =>
			parameters
				.Add(section => section.App, ArrApp.Sonarr)
				.Add(section => section.Instance, instance)
				.Add(section => section.State, ArrConnectionState.Connected)
				.Add(section => section.Choices, Choices)
				.Add(section => section.InstanceChanged, changes.Add)
		);
	}

	private IRenderedComponent<ArrAppSection> RenderSection(ArrInstanceConfig instance, ArrConnectionState state, string error = "", ArrApp app = ArrApp.Sonarr)
	{
		return Render<ArrAppSection>(parameters =>
			parameters
				.Add(section => section.App, app)
				.Add(section => section.Instance, instance)
				.Add(section => section.State, state)
				.Add(section => section.Error, error)
				.Add(section => section.Choices, Choices)
				.Add(section => section.Test, typed => _tested.Add(typed))
		);
	}
}
