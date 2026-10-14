using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;

namespace Licences;

internal sealed record Package(string Id, string Version, string Project, string Copyright);

internal sealed record Licence(string Id, string Text, IReadOnlyList<Package> Packages);

internal sealed record Asset(string File, string Origin, string Author, string Licence, string Changes);

internal sealed record Notices(IReadOnlyList<Licence> Licences, IReadOnlyList<Asset> Assets);

internal sealed record ShippedPackage(Package Package, string? Expression);

internal sealed record LicenceOverride(string Id, string Version, string License);

internal static class ThirdPartyNotices
{
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		NewLine = "\n",
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	internal static void Write(string depsFile, string packageRoot)
	{
		var notices = Collect(
			Shipped(depsFile, packageRoot),
			Read<LicenceOverride[]>(".github/licences/overrides.json"),
			id => File.ReadAllText($".github/licences/texts/{id}.txt"),
			[.. Assets("UI/wwwroot/providers/credits.json", "providers/"), .. Assets(".github/licences/assets.json", "")]
		);
		var output = Path.GetDirectoryName(depsFile)!;
		File.WriteAllText(Path.Combine(output, "third-party-notices.json"), Json(notices));
		File.WriteAllText(Path.Combine(output, "THIRD-PARTY-NOTICES.txt"), Text(notices, File.ReadAllText(".github/licences/runtime.txt")));
	}

	internal static Notices Collect(
		IEnumerable<ShippedPackage> packages,
		IReadOnlyCollection<LicenceOverride> overrides,
		Func<string, string> text,
		IEnumerable<Asset> assets
	) =>
		new(
			[
				.. packages
					.GroupBy(package => LicenceOf(package, overrides), StringComparer.Ordinal)
					.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
					.Select(group => new Licence(
						group.Key,
						text(group.Key),
						[
							.. group
								.Select(package => package.Package)
								.OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
								.ThenBy(package => package.Version, StringComparer.Ordinal),
						]
					)),
			],
			[.. assets.OrderBy(asset => asset.File, StringComparer.Ordinal).ThenBy(asset => asset.Origin, StringComparer.Ordinal)]
		);

	internal static string Json(Notices notices) => JsonSerializer.Serialize(notices, Options) + "\n";

	internal static string Text(Notices notices, string runtime)
	{
		var text = new StringBuilder("Sammlarr third-party notices\n\n");
		text.Append("Sammlarr ships the NuGet packages, assets and runtime below. Each keeps its own licence and copyright.\n\n\n");
		text.Append("NuGet packages\n\n");
		var packages = notices
			.Licences.SelectMany(licence => licence.Packages, (licence, package) => (Licence: licence.Id, Package: package))
			.OrderBy(entry => entry.Package.Id, StringComparer.OrdinalIgnoreCase)
			.ThenBy(entry => entry.Package.Version, StringComparer.Ordinal);
		foreach (var (licence, package) in packages)
		{
			text.Append($"{package.Id} {package.Version}\n  Licence: {licence}\n");
			Line(text, "  Project: ", package.Project);
			Line(text, "  ", package.Copyright);
			text.Append('\n');
		}
		text.Append("\nLicence texts\n\n");
		foreach (var licence in notices.Licences)
		{
			text.Append($"{licence.Id}\n\n{licence.Text}\n\n");
		}
		text.Append("\nAssets\n\nEvery provider logo stays a trademark of its owner.\n\n");
		foreach (var asset in notices.Assets)
		{
			text.Append($"{asset.File}\n  Origin: {asset.Origin}\n  Author: {asset.Author}\n  Licence: {asset.Licence}\n  Changes: {asset.Changes}\n\n");
		}
		return text.Append('\n').Append(runtime).ToString();
	}

	internal static ShippedPackage Nuspec(string name, string nuspec)
	{
		var root = XDocument.Parse(nuspec).Root!;
		var metadata = root.Element(root.Name.Namespace + "metadata")!;
		string? Value(string element) => (string?)metadata.Element(root.Name.Namespace + element);
		var licence = metadata.Element(root.Name.Namespace + "license");
		var expression = string.Equals((string?)licence?.Attribute("type"), "expression", StringComparison.Ordinal) ? licence!.Value : null;
		var project = Value("projectUrl") ?? (string?)metadata.Element(root.Name.Namespace + "repository")?.Attribute("url") ?? "";
		return new ShippedPackage(new Package(name.Split('/')[0], name.Split('/')[1], project, (Value("copyright") ?? "").Trim()), expression);
	}

	private static List<ShippedPackage> Shipped(string depsFile, string packageRoot)
	{
		using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));
		return
		[
			.. deps
				.RootElement.GetProperty("libraries")
				.EnumerateObject()
				.Where(library => library.Value.GetProperty("type").ValueEquals("package"))
				.Select(library =>
					Nuspec(
						library.Name,
						File.ReadAllText(Directory.EnumerateFiles(Path.Combine(packageRoot, library.Value.GetProperty("path").GetString()!), "*.nuspec").Single())
					)
				),
		];
	}

	private static string LicenceOf(ShippedPackage package, IEnumerable<LicenceOverride> overrides) =>
		overrides
			.FirstOrDefault(entry =>
				string.Equals(entry.Id, package.Package.Id, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(entry.Version, package.Package.Version, StringComparison.Ordinal)
			)
			?.License
		?? package.Expression?.Split(" OR ")[0]
		?? throw new InvalidOperationException(
			$"{package.Package.Id} {package.Package.Version} declares no licence expression; add it to .github/licences/overrides.json"
		);

	private static IEnumerable<Asset> Assets(string credits, string folder) =>
		Read<Credit[]>(credits).Select(credit => new Asset(folder + credit.File, credit.Sources[0], credit.Author, credit.Licence, credit.Changes));

	private static void Line(StringBuilder text, string prefix, string value)
	{
		if (value.Length > 0)
		{
			text.Append($"{prefix}{value}\n");
		}
	}

	private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)!;

	private sealed record Credit(string File, IReadOnlyList<string> Sources, string Author, string Licence, string Changes);
}
