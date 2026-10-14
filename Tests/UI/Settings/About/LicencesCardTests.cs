using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.About;

namespace UI.Tests.Settings.About;

public class LicencesCardTests : BunitContext
{
	private static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "Settings", "About");

	private static readonly string LocalBuild = Path.Combine(AppContext.BaseDirectory, "Settings", "About", "no-notices");

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Head_CountsThePackagesAndTheirLicences_AndOffersTheNoticesFileForDownload()
	{
		var cut = Loaded(Shipped);

		var notices = cut.Find("a[download]");
		await Assert.That(Text(cut.Find("div > div"))).IsEqualTo("Licences 8 packages under 3 licences THIRD-PARTY-NOTICES");
		await Assert.That(notices.GetAttribute("href")).IsEqualTo("/THIRD-PARTY-NOTICES.txt");
	}

	[Test]
	public async Task Groups_OnePerLicenceWithTheMostPackagesFirst_NameTheCountAndTheFirstPackages()
	{
		var cut = Loaded(Shipped);

		await Assert
			.That(Groups(cut).Select(Text))
			.IsEquivalentTo(
				[
					"MIT 5 packages Cronos, Fluid.Core, Microsoft.Extensions.Http.Resilience, WolverineFx ...",
					"Apache-2.0 2 packages Serilog, Vogen",
					"MIT or Apache-2.0 1 package TickerQ",
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Groups_StayClosedUntilOpened()
	{
		var cut = Loaded(Shipped);

		await Assert.That(Groups(cut).Select(group => group.GetAttribute("aria-expanded") ?? string.Empty)).IsEquivalentTo(["false", "false", "false"]);
		await Assert.That(cut.FindAll("li")).IsEmpty();
		await Assert.That(cut.FindAll("pre")).IsEmpty();
	}

	[Test]
	public async Task Group_WhenOpened_ListsEachPackageWithItsVersionAndCopyright_ThenTheLicenceTextOnce()
	{
		var cut = Loaded(Shipped);

		await OpenAsync(cut, "MIT 5 packages");

		await Assert
			.That(cut.FindAll("li").Select(Text))
			.IsEquivalentTo(
				[
					"Cronos 0.11.1 Copyright (c) 2017 Hangfire OÜ",
					"Fluid.Core 2.31.0 Sebastien Ros",
					"Microsoft.Extensions.Http.Resilience 10.7.0 © Microsoft Corporation",
					"WolverineFx 6.28.2 Jeremy D. Miller, Babu Annamalai, Jaedyn Tonee",
					"WolverineFx.Sqlite 6.28.2",
				],
				CollectionOrdering.Matching
			);
		await Assert.That(cut.FindAll("pre").Single().TextContent).StartsWith("MIT License\n\nPermission is hereby granted");
	}

	[Test]
	public async Task Packages_WithAProject_LinkItInANewTab_AndOneWithoutStaysPlainText()
	{
		var cut = Loaded(Shipped);

		await OpenAsync(cut, "MIT 5 packages");

		var links = cut.FindAll("li a");
		await Assert
			.That(links.Select(link => $"{Text(link)} {link.GetAttribute("href")}"))
			.IsEquivalentTo(
				[
					"Cronos https://github.com/HangfireIO/Cronos",
					"Fluid.Core https://github.com/sebastienros/fluid",
					"Microsoft.Extensions.Http.Resilience https://dot.net/",
					"WolverineFx https://wolverinefx.net",
				],
				CollectionOrdering.Matching
			);
		await Assert.That(links.Select(link => link.GetAttribute("target") ?? string.Empty)).IsEquivalentTo(Enumerable.Repeat("_blank", links.Count));
	}

	[Test]
	public async Task Head_ForASinglePackage_SpeaksInTheSingular()
	{
		var directory = Directory.CreateTempSubdirectory("sammlarr-notices-").FullName;
		await File.WriteAllTextAsync(
			Path.Combine(directory, "third-party-notices.json"),
			"""{ "licences": [ { "id": "MIT", "text": "MIT License", "packages": [ { "id": "Cronos", "version": "0.11.1", "project": "", "copyright": "" } ] } ], "assets": [] }"""
		);

		var cut = Loaded(directory);

		await Assert.That(Text(cut.Find("div > div"))).IsEqualTo("Licences 1 package under 1 licence THIRD-PARTY-NOTICES");
	}

	[Test]
	public async Task LocalBuild_WithoutTheNoticesFile_SaysWhyAndLinksTheLatestReleasesList()
	{
		var cut = Loaded(LocalBuild);

		await Assert
			.That(Text(cut.Find("div")))
			.IsEqualTo(
				"Licences No licence list in this build It is generated when Sammlarr is published, so a local build runs without it. The list for the latest release is on GitHub."
			);
		await Assert
			.That(cut.FindAll("a").Select(link => link.GetAttribute("href") ?? string.Empty))
			.IsEquivalentTo(["https://github.com/daerup/Sammlarr/releases/latest"]);
		await Assert.That(cut.FindAll("button")).IsEmpty();
	}

	[Test]
	public async Task LocalBuild_WithoutARepository_SaysWhyWithoutALink()
	{
		var cut = Loaded(LocalBuild, string.Empty);

		await Assert
			.That(Text(cut.Find("div")))
			.IsEqualTo("Licences No licence list in this build It is generated when Sammlarr is published, so a local build runs without it.");
		await Assert.That(cut.FindAll("a")).IsEmpty();
	}

	private IRenderedComponent<LicencesCard> Loaded(string directory, string repository = "daerup/Sammlarr")
	{
		Services.AddSingleton(new ThirdPartyNotices(directory));
		var cut = Render<LicencesCard>(parameters => parameters.Add(card => card.Repository, repository));
		cut.WaitForState(() => cut.Markup.Contains("Licences", StringComparison.Ordinal));
		return cut;
	}

	private static IReadOnlyList<IElement> Groups(IRenderedComponent<LicencesCard> cut) => cut.FindAll("button[aria-expanded]");

	private static async Task OpenAsync(IRenderedComponent<LicencesCard> cut, string group)
	{
		await Groups(cut).Single(button => Text(button).StartsWith(group, StringComparison.Ordinal)).ClickAsync();
	}

	private static string Text(IElement element) => string.Join(' ', element.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
