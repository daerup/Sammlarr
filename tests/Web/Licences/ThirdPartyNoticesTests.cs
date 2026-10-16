using System.Text.Json;
using Licences;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Web.Tests.Licences;

public class ThirdPartyNoticesTests
{
	private static readonly Dictionary<string, string> Texts = new()
	{
		["Apache-2.0"] = "Apache text",
		["blessing"] = "blessing text",
		["BSD-2-Clause"] = "BSD text",
		["MIT"] = "MIT text",
		["OFL-1.1"] = "OFL text",
	};

	private static readonly Dictionary<string, string> Copyrights = new() { ["WolverineFx"] = "Copyright (c) Jeremy D. Miller and Contributors." };

	private static ShippedPackage Shipped(string id, string version, string? expression, string copyright = "") =>
		new(new Package(id, version, "", copyright, ""), expression);

	private static Notices Collect(IEnumerable<ShippedPackage> packages, params LicenceOverride[] overrides)
	{
		return ThirdPartyNotices.Collect(packages, overrides, Copyrights, Texts, []);
	}

	[Test]
	public async Task Collect_PackagesUnderThreeLicences_GroupsThemOncePerLicenceSortedById()
	{
		var notices = Collect([
			Shipped("Serilog", "4.4.0", "Apache-2.0"),
			Shipped("Cronos", "0.13.0", "MIT"),
			Shipped("Markdig", "0.37.0", "BSD-2-Clause"),
			Shipped("Polly.Core", "8.6.5", "MIT"),
		]);

		await Assert.That(notices.Licences.Select(licence => licence.Id)).IsEquivalentTo(["Apache-2.0", "BSD-2-Clause", "MIT"], CollectionOrdering.Matching);
		await Assert.That(notices.Licences[2].Text).IsEqualTo("MIT text");
		await Assert.That(notices.Licences[2].Packages.Select(package => package.Id)).IsEquivalentTo(["Cronos", "Polly.Core"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Collect_PackagesOfOneLicence_SortsThemByIdIgnoringCaseThenByVersion()
	{
		var notices = Collect([
			Shipped("Zeta", "1.0.0", "MIT"),
			Shipped("beta", "2.0.0", "MIT"),
			Shipped("Alpha", "1.0.0", "MIT"),
			Shipped("beta", "1.0.0", "MIT"),
		]);

		await Assert
			.That(notices.Licences.Single().Packages.Select(package => $"{package.Id} {package.Version}"))
			.IsEquivalentTo(["Alpha 1.0.0", "beta 1.0.0", "beta 2.0.0", "Zeta 1.0.0"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Collect_PackageWithOnlyALicenceUrl_LandsUnderTheLicenceItsOverrideNames()
	{
		var notices = Collect(
			[Shipped("ncrontab", "3.3.0", null), Shipped("SQLite", "3.53.4", "Unknown")],
			new LicenceOverride("NCrontab", "3.3.0", "Apache-2.0"),
			new LicenceOverride("SQLite", "3.53.4", "blessing")
		);

		await Assert
			.That(notices.Licences.Select(licence => $"{licence.Id}: {licence.Packages.Single().Id}"))
			.IsEquivalentTo(["Apache-2.0: ncrontab", "blessing: SQLite"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Collect_OverrideForAnotherVersion_FailsNamingThePackageAndTheOverridesFile()
	{
		await Assert
			.That(() => Collect([Shipped("ncrontab", "3.4.0", null)], new LicenceOverride("ncrontab", "3.3.0", "Apache-2.0")))
			.Throws<InvalidOperationException>()
			.WithMessageContaining(
				"ncrontab 3.4.0 declares no licence with a text in .github/licences/texts (no expression); add it to .github/licences/overrides.json"
			);
	}

	[Test]
	public async Task Collect_PackageWithoutACopyright_TakesItFromTheCopyrightsFile()
	{
		var notices = Collect([Shipped("WolverineFx", "6.49.0", "MIT")]);

		await Assert.That(notices.Licences.Single().Packages.Single().Copyright).IsEqualTo("Copyright (c) Jeremy D. Miller and Contributors.");
	}

	[Test]
	public async Task Collect_PackageWithItsOwnCopyright_KeepsIt()
	{
		var notices = Collect([Shipped("WolverineFx", "6.49.0", "MIT", "Copyright (c) the package")]);

		await Assert.That(notices.Licences.Single().Packages.Single().Copyright).IsEqualTo("Copyright (c) the package");
	}

	[Test]
	[Arguments("MIT OR Apache-2.0", "MIT")]
	[Arguments("LGPL-2.1-only OR MIT", "MIT")]
	public async Task Collect_DualLicensedPackage_LandsUnderTheFirstChoiceWithAText(string expression, string expected)
	{
		var notices = Collect([Shipped("TickerQ", "10.4.0", expression)]);

		await Assert.That(notices.Licences.Single().Id).IsEqualTo(expected);
	}

	[Test]
	public async Task Collect_Assets_SortsThemByFileThenOrigin()
	{
		Asset Asset(string file, string origin) => new(file, origin, "", "", "");

		var notices = ThirdPartyNotices.Collect(
			[],
			[],
			Copyrights,
			Texts,
			[
				Asset("providers/tmdb.svg", "https://tmdb.org"),
				Asset("css/app.css", "https://github.com/tailwindlabs"),
				Asset("css/app.css", "https://github.com/sindresorhus"),
			]
		);

		await Assert
			.That(notices.Assets.Select(asset => $"{asset.File} {asset.Origin}"))
			.IsEquivalentTo(
				["css/app.css https://github.com/sindresorhus", "css/app.css https://github.com/tailwindlabs", "providers/tmdb.svg https://tmdb.org"],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Shipped_DepsWithAProjectAPackageAndARuntimePack_ReadsOnlyThePackageFromItsFolder()
	{
		const string deps = """
			{
			  "libraries": {
			    "Core/1.0.0": { "type": "project", "serviceable": false, "sha512": "" },
			    "Cronos/0.13.0": { "type": "package", "serviceable": true, "path": "cronos/0.13.0" },
			    "runtimepack.Microsoft.NETCore.App.Runtime.linux-x64/11.0.0": { "type": "runtimepack", "serviceable": false, "sha512": "" }
			  }
			}
			""";
		const string cronos = """
			<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
			  <metadata>
			    <id>Cronos</id>
			    <license type="expression">MIT</license>
			    <projectUrl>https://github.com/HangfireIO/Cronos</projectUrl>
			    <copyright> Copyright © 2016 Hangfire OÜ. </copyright>
			  </metadata>
			</package>
			""";
		Dictionary<string, string> nuspecs = new() { ["cronos/0.13.0"] = cronos };
		Dictionary<string, string> notices = new() { ["cronos/0.13.0"] = "NOTICE\n\nCronos notice" };

		var packages = ThirdPartyNotices.Shipped(deps, path => nuspecs[path], path => notices[path]);

		await Assert
			.That(packages)
			.IsEquivalentTo([
				new ShippedPackage(
					new Package("Cronos", "0.13.0", "https://github.com/HangfireIO/Cronos", "Copyright © 2016 Hangfire OÜ.", "NOTICE\n\nCronos notice"),
					"MIT"
				),
			]);
	}

	[Test]
	public async Task Shipped_PackageFolderWithoutANuspec_FailsNamingThePackage()
	{
		const string deps = """{ "libraries": { "Cronos/0.13.0": { "type": "package", "path": "cronos/0.13.0" } } }""";

		await Assert
			.That(() => ThirdPartyNotices.Shipped(deps, _ => null, _ => ""))
			.Throws<InvalidOperationException>()
			.WithMessageContaining("Cronos/0.13.0 has no nuspec in its package folder cronos/0.13.0");
	}

	[Test]
	public async Task Shipped_PackageWithALicenceFileAndOnlyARepository_HasNoExpressionAndTheRepositoryAsProject()
	{
		const string deps = """{ "libraries": { "SQLite/3.53.4": { "type": "package", "path": "sqlite/3.53.4" } } }""";
		const string nuspec = """
			<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
			  <metadata>
			    <id>SQLite</id>
			    <license type="file">LICENSE.txt</license>
			    <repository type="git" url="https://github.com/ericsink/SQLitePCL.raw" />
			  </metadata>
			</package>
			""";

		var packages = ThirdPartyNotices.Shipped(deps, _ => nuspec, _ => "");

		await Assert
			.That(packages)
			.IsEquivalentTo([new ShippedPackage(new Package("SQLite", "3.53.4", "https://github.com/ericsink/SQLitePCL.raw", "", ""), null)]);
	}

	[Test]
	public async Task Notice_TwoFilesWithWindowsLineEndings_JoinsThemSortedByNameWithUnixLineEndings()
	{
		var notice = ThirdPartyNotices.Notice([("THIRD-PARTY-NOTICES.TXT", "Skia\r\nHarfBuzz\r\n"), ("NOTICE.txt", "Blazor Blueprint\r\n")]);

		await Assert.That(notice).IsEqualTo("NOTICE.txt\n\nBlazor Blueprint\n\nTHIRD-PARTY-NOTICES.TXT\n\nSkia\nHarfBuzz");
	}

	[Test]
	public async Task Json_ForTheCard_WritesTheContractFieldsInOrderWithoutTheNoticeOrEscapingText()
	{
		var notices = new Notices(
			[
				new Licence(
					"MIT",
					"MIT text",
					[new Package("Cronos", "0.13.0", "https://github.com/HangfireIO/Cronos", "Copyright © Hangfire OÜ", "NOTICE\n\nCronos notice")]
				),
			],
			[new Asset("providers/tmdb.svg", "https://tmdb.org", "TMDB", "TMDB API terms of use", "none")]
		);

		await Assert
			.That(ThirdPartyNotices.Json(notices))
			.IsEqualTo(
				"""
				{
				  "licences": [
				    {
				      "id": "MIT",
				      "text": "MIT text",
				      "packages": [
				        {
				          "id": "Cronos",
				          "version": "0.13.0",
				          "project": "https://github.com/HangfireIO/Cronos",
				          "copyright": "Copyright © Hangfire OÜ"
				        }
				      ]
				    }
				  ],
				  "assets": [
				    {
				      "file": "providers/tmdb.svg",
				      "origin": "https://tmdb.org",
				      "author": "TMDB",
				      "licence": "TMDB API terms of use",
				      "changes": "none"
				    }
				  ]
				}

				""".ReplaceLineEndings("\n")
			);
	}

	[Test]
	public async Task Text_PackageWithoutProjectOrCopyright_ListsIdVersionAndLicenceOnlyAndEndsWithTheRuntimePart()
	{
		var notices = new Notices([new Licence("MIT", "MIT text", [new Package("TailwindMerge.NET", "1.3.0", "", "", "")])], []);

		var text = ThirdPartyNotices.Text(notices, Texts, "Runtime and base image\n");

		await Assert.That(text).Contains("\nTailwindMerge.NET 1.3.0\n  Licence: MIT\n\n");
		await Assert.That(text).Contains("\nMIT\n\nMIT text\n");
		await Assert.That(text).EndsWith("\nRuntime and base image\n");
	}

	[Test]
	public async Task Text_PackageWithANotice_PrintsTheNoticeIndentedUnderThePackage()
	{
		var notices = new Notices(
			[
				new Licence(
					"Apache-2.0",
					"Apache text",
					[new Package("BlazorBlueprint.Components", "4.1.0", "", "Copyright (c) Mathew Taylor", "NOTICE\n\nBlazor Blueprint")]
				),
			],
			[]
		);

		var text = ThirdPartyNotices.Text(notices, Texts, "");

		await Assert
			.That(text)
			.Contains("\nBlazorBlueprint.Components 4.1.0\n  Licence: Apache-2.0\n  Copyright (c) Mathew Taylor\n\n    NOTICE\n\n    Blazor Blueprint\n\n");
	}

	[Test]
	public async Task Text_TwoAssetsUnderALicenceNoPackageUses_PrintThatLicenceTextOnce()
	{
		var notices = new Notices(
			[new Licence("MIT", "MIT text", [new Package("Cronos", "0.13.0", "", "", "")])],
			[new Asset("fonts/roboto", "", "", "OFL-1.1", ""), new Asset("fonts/roboto-mono", "", "", "OFL-1.1", "")]
		);

		var text = ThirdPartyNotices.Text(notices, Texts, "");

		await Assert.That(text.Split("\nOFL-1.1\n\nOFL text\n")).Count().IsEqualTo(2);
	}

	[Test]
	public async Task AllowedLicences_InTheLicenceGate_EachShipTheirText()
	{
		var allowed = JsonSerializer.Deserialize<string[]>(File.ReadAllText(RepositoryRoot.Resolve(".github", "licences", "allowed.json")))!;

		await Assert.That(allowed.Where(id => !File.Exists(RepositoryRoot.Resolve(".github", "licences", "texts", $"{id}.txt")))).IsEmpty();
	}
}
