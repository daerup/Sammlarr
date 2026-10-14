using System.Text.Json;
using Licences;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Web.Tests.Licences;

public class ThirdPartyNoticesTests
{
	private static ShippedPackage Shipped(string id, string version, string? expression) => new(new Package(id, version, "", ""), expression);

	private static Notices Collect(IEnumerable<ShippedPackage> packages, params LicenceOverride[] overrides) =>
		ThirdPartyNotices.Collect(packages, overrides, id => $"{id} text", []);

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
			.WithMessageContaining("ncrontab 3.4.0 declares no licence expression; add it to .github/licences/overrides.json");
	}

	[Test]
	public async Task Collect_DualLicensedPackage_LandsUnderItsFirstChoice()
	{
		var notices = Collect([Shipped("TickerQ", "10.4.0", "MIT OR Apache-2.0")]);

		await Assert.That(notices.Licences.Single().Id).IsEqualTo("MIT");
	}

	[Test]
	public async Task Collect_Assets_SortsThemByFileThenOrigin()
	{
		Asset Asset(string file, string origin) => new(file, origin, "", "", "");

		var notices = ThirdPartyNotices.Collect(
			[],
			[],
			id => id,
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
	public async Task Nuspec_WithALicenceExpression_ReadsTheExpressionProjectAndTrimmedCopyright()
	{
		const string nuspec = """
			<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
			  <metadata>
			    <id>Cronos</id>
			    <license type="expression">MIT</license>
			    <projectUrl>https://github.com/HangfireIO/Cronos</projectUrl>
			    <copyright> Copyright © 2016 Hangfire OÜ. </copyright>
			  </metadata>
			</package>
			""";

		var package = ThirdPartyNotices.Nuspec("Cronos/0.13.0", nuspec);

		await Assert
			.That(package)
			.IsEqualTo(new ShippedPackage(new Package("Cronos", "0.13.0", "https://github.com/HangfireIO/Cronos", "Copyright © 2016 Hangfire OÜ."), "MIT"));
	}

	[Test]
	public async Task Nuspec_WithALicenceFileAndOnlyARepository_HasNoExpressionAndTheRepositoryAsProject()
	{
		const string nuspec = """
			<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
			  <metadata>
			    <id>SQLite</id>
			    <license type="file">LICENSE.txt</license>
			    <repository type="git" url="https://github.com/ericsink/SQLitePCL.raw" />
			  </metadata>
			</package>
			""";

		var package = ThirdPartyNotices.Nuspec("SQLite/3.53.4", nuspec);

		await Assert.That(package).IsEqualTo(new ShippedPackage(new Package("SQLite", "3.53.4", "https://github.com/ericsink/SQLitePCL.raw", ""), null));
	}

	[Test]
	public async Task Json_ForTheCard_WritesTheContractFieldsInOrderWithoutEscapingText()
	{
		var notices = new Notices(
			[new Licence("MIT", "MIT text", [new Package("Cronos", "0.13.0", "https://github.com/HangfireIO/Cronos", "Copyright © Hangfire OÜ")])],
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
		var notices = new Notices([new Licence("MIT", "MIT text", [new Package("TailwindMerge.NET", "1.3.0", "", "")])], []);

		var text = ThirdPartyNotices.Text(notices, "Runtime and base image\n");

		await Assert.That(text).Contains("\nTailwindMerge.NET 1.3.0\n  Licence: MIT\n\n");
		await Assert.That(text).Contains("\nMIT\n\nMIT text\n");
		await Assert.That(text).EndsWith("\nRuntime and base image\n");
	}

	[Test]
	public async Task AllowedLicences_InTheLicenceGate_EachShipTheirText()
	{
		var allowed = JsonSerializer.Deserialize<string[]>(File.ReadAllText(RepositoryRoot.Resolve(".github", "licences", "allowed.json")))!;

		await Assert.That(allowed.Where(id => !File.Exists(RepositoryRoot.Resolve(".github", "licences", "texts", $"{id}.txt")))).IsEmpty();
	}
}
