using BlazorBlueprint.Components;
using Bunit;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.About;
using UI.Shared.Services;

namespace UI.Tests.Settings.About;

public class VersionCardTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Card_BeforeAnyRelease_MarksADevBuildAndSaysTheNumberCountsTheCommits()
	{
		var cut = Render<VersionCard>(parameters =>
			parameters.Add(card => card.Version, new AppVersion("0.0.0-alpha.0.584")).Add(card => card.Repository, "daerup/Sammlarr")
		);

		await Assert.That(cut.Find(".version-number").TextContent).IsEqualTo("0.0.0-alpha.0.584");
		await Assert.That(cut.Markup).Contains("Dev build");
		await Assert.That(cut.Find(".version-source").TextContent).Contains("so far: 584");
		await Assert.That(cut.FindAll("a").Select(link => link.GetAttribute("href") ?? string.Empty)).IsEquivalentTo(["https://github.com/daerup/Sammlarr"]);
	}

	[Test]
	public async Task Card_OneCommitAfterARelease_SaysSoInTheSingular()
	{
		var cut = Render<VersionCard>(parameters =>
			parameters.Add(card => card.Version, new AppVersion("1.4.1-alpha.0.1")).Add(card => card.Repository, "daerup/Sammlarr")
		);

		await Assert.That(cut.Find(".version-source").TextContent).Contains("1 commit past");
	}

	[Test]
	[Arguments("1.4.0")]
	[Arguments("2.0.0-rc.1")]
	public async Task Card_ForATaggedBuild_NamesItsTagAndLinksItsReleaseNotesThenTheRepository(string number)
	{
		var cut = Render<VersionCard>(parameters => parameters.Add(card => card.Version, new AppVersion(number)).Add(card => card.Repository, "daerup/Sammlarr"));

		await Assert.That(cut.Markup).DoesNotContain("Dev build");
		await Assert.That(cut.Find(".version-source").TextContent).Contains($"release tag v{number}");
		await Assert
			.That(cut.FindAll("a").Select(link => link.GetAttribute("href") ?? string.Empty))
			.IsEquivalentTo([$"https://github.com/daerup/Sammlarr/releases/tag/v{number}", "https://github.com/daerup/Sammlarr"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Card_WithoutARepository_ShowsTheVersionButNoGitHubLinks()
	{
		var cut = Render<VersionCard>(parameters => parameters.Add(card => card.Version, new AppVersion("1.4.0")).Add(card => card.Repository, string.Empty));

		await Assert.That(cut.Find(".version-number").TextContent).IsEqualTo("1.4.0");
		await Assert.That(cut.FindAll("a")).IsEmpty();
	}

	[Test]
	public async Task Card_WhenANewerReleaseIsOut_ShowsItsBadgeAndLinksWhatIsNewBesideTheRunningReleasesNotes()
	{
		var update = new AppUpdate("1.5.0", new Uri("https://github.com/daerup/Sammlarr/releases/tag/v1.5.0"));

		var cut = Render<VersionCard>(parameters =>
			parameters.Add(card => card.Version, new AppVersion("1.4.0")).Add(card => card.Repository, "daerup/Sammlarr").Add(card => card.Update, Some.Of(update))
		);

		await Assert.That(cut.Find("[data-testid=version-update]").TextContent.Trim()).IsEqualTo("1.5.0 available");
		await Assert
			.That(cut.FindAll("a").Select(link => link.GetAttribute("href") ?? string.Empty))
			.IsEquivalentTo([
				"https://github.com/daerup/Sammlarr/releases/tag/v1.5.0",
				"https://github.com/daerup/Sammlarr/releases/tag/v1.4.0",
				"https://github.com/daerup/Sammlarr",
			]);
	}

	[Test]
	public async Task Card_WhileUpToDate_ShowsNoUpdateBadge()
	{
		var cut = Render<VersionCard>(parameters => parameters.Add(card => card.Version, new AppVersion("1.4.0")).Add(card => card.Repository, "daerup/Sammlarr"));

		await Assert.That(cut.FindAll("[data-testid=version-update]")).IsEmpty();
	}
}
