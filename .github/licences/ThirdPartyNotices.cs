using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Licences;

internal sealed record Package(string Id, string Version, string Project, string Copyright, [property: JsonIgnore] string Notice);

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
		var texts = Directory.EnumerateFiles(".github/licences/texts").ToDictionary(file => Path.GetFileNameWithoutExtension(file), File.ReadAllText);
		var notices = Collect(
			Shipped(File.ReadAllText(depsFile), path => NuspecIn(Path.Combine(packageRoot, path)), path => NoticeIn(Path.Combine(packageRoot, path))),
			Read<LicenceOverride[]>(".github/licences/overrides.json"),
			Read<Dictionary<string, string>>(".github/licences/copyrights.json"),
			texts,
			[.. Assets("src/UI/wwwroot/providers/credits.json", "providers/"), .. Assets(".github/licences/assets.json", "")]
		);
		var output = Path.GetDirectoryName(depsFile)!;
		File.WriteAllText(Path.Combine(output, "third-party-notices.json"), Json(notices));
		File.WriteAllText(Path.Combine(output, "THIRD-PARTY-NOTICES.txt"), Text(notices, texts, File.ReadAllText(".github/licences/runtime.txt")));
	}

	internal static Notices Collect(
		IEnumerable<ShippedPackage> packages,
		IReadOnlyCollection<LicenceOverride> overrides,
		IReadOnlyDictionary<string, string> copyrights,
		IReadOnlyDictionary<string, string> texts,
		IEnumerable<Asset> assets
	)
	{
		return new(
			[
				.. packages
					.Select(package => WithCopyright(package, copyrights))
					.GroupBy(package => LicenceOf(package, overrides, texts))
					.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
					.Select(group => new Licence(
						group.Key,
						texts[group.Key],
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
	}

	internal static List<ShippedPackage> Shipped(string deps, Func<string, string?> nuspec, Func<string, string> notice)
	{
		using var document = JsonDocument.Parse(deps);
		return
		[
			.. document
				.RootElement.GetProperty("libraries")
				.EnumerateObject()
				.Where(library => library.Value.GetProperty("type").ValueEquals("package"))
				.Select(library =>
				{
					var folder = library.Value.GetProperty("path").GetString()!;
					var manifest = nuspec(folder) ?? throw new InvalidOperationException($"{library.Name} has no nuspec in its package folder {folder}");
					return Nuspec(library.Name, manifest, notice(folder));
				}),
		];
	}

	private static ShippedPackage Nuspec(string name, string nuspec, string notice)
	{
		var root = XDocument.Parse(nuspec).Root!;
		var metadata = root.Element(root.Name.Namespace + "metadata")!;
		string? Value(string element) => (string?)metadata.Element(root.Name.Namespace + element);
		var licence = metadata.Element(root.Name.Namespace + "license");
		var expression = (string?)licence?.Attribute("type") == "expression" ? licence!.Value : null;
		var project = Value("projectUrl") ?? (string?)metadata.Element(root.Name.Namespace + "repository")?.Attribute("url") ?? "";
		var parts = name.Split('/');
		return new ShippedPackage(new Package(parts[0], parts[1], project, (Value("copyright") ?? "").Trim(), notice), expression);
	}

	internal static string Json(Notices notices) => JsonSerializer.Serialize(notices, Options) + "\n";

	internal static string Text(Notices notices, IReadOnlyDictionary<string, string> texts, string runtime)
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
			Line(text, "\n", Indented(package.Notice));
			text.Append('\n');
		}
		text.Append("\nLicence texts\n\n");
		var licences = notices
			.Licences.Select(licence => licence.Id)
			.Concat(notices.Assets.Select(asset => asset.Licence).Where(texts.ContainsKey))
			.Distinct()
			.Order(StringComparer.OrdinalIgnoreCase);
		foreach (var licence in licences)
		{
			text.Append($"{licence}\n\n{texts[licence]}\n\n");
		}
		text.Append("\nAssets\n\nEvery provider logo stays a trademark of its owner.\n\n");
		foreach (var asset in notices.Assets)
		{
			text.Append($"{asset.File}\n  Origin: {asset.Origin}\n  Author: {asset.Author}\n  Licence: {asset.Licence}\n  Changes: {asset.Changes}\n\n");
		}
		return text.Append('\n').Append(runtime).ToString();
	}

	private static string LicenceOf(ShippedPackage package, IEnumerable<LicenceOverride> overrides, IReadOnlyDictionary<string, string> texts)
	{
		var overridden = overrides.FirstOrDefault(entry =>
			string.Equals(entry.Id, package.Package.Id, StringComparison.OrdinalIgnoreCase) && entry.Version == package.Package.Version
		);
		return overridden?.License
			?? package.Expression?.Split(" OR ").FirstOrDefault(texts.ContainsKey)
			?? throw new InvalidOperationException(
				$"{package.Package.Id} {package.Package.Version} declares no licence with a text in .github/licences/texts ({package.Expression ?? "no expression"}); add it to .github/licences/overrides.json"
			);
	}

	private static ShippedPackage WithCopyright(ShippedPackage shipped, IReadOnlyDictionary<string, string> copyrights)
	{
		return shipped.Package.Copyright.Length == 0 && copyrights.TryGetValue(shipped.Package.Id, out var copyright)
			? shipped with
			{
				Package = shipped.Package with { Copyright = copyright },
			}
			: shipped;
	}

	private static string? NuspecIn(string folder) => Directory.EnumerateFiles(folder, "*.nuspec").Select(File.ReadAllText).SingleOrDefault();

	internal static string Notice(IEnumerable<(string Name, string Text)> files)
	{
		return string.Join(
			"\n\n",
			files
				.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
				.ThenBy(file => file.Name, StringComparer.Ordinal)
				.Select(file => $"{file.Name}\n\n{file.Text.ReplaceLineEndings("\n").Trim()}")
		);
	}

	private static string NoticeIn(string folder)
	{
		var files = Directory
			.EnumerateFiles(folder)
			.Select(file => (Name: Path.GetFileName(file), Path: file))
			.Where(file =>
				file.Name.StartsWith("NOTICE", StringComparison.OrdinalIgnoreCase) || file.Name.StartsWith("THIRD-PARTY-NOTICES", StringComparison.OrdinalIgnoreCase)
			);
		return Notice(files.Select(file => (file.Name, File.ReadAllText(file.Path))));
	}

	private static string Indented(string notice)
	{
		return string.Join('\n', notice.Split('\n').Select(line => line.Length > 0 ? $"    {line}" : line));
	}

	private static IEnumerable<Asset> Assets(string credits, string folder)
	{
		return Read<Credit[]>(credits).Select(credit => new Asset(folder + credit.File, credit.Sources[0], credit.Author, credit.Licence, credit.Changes));
	}

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
