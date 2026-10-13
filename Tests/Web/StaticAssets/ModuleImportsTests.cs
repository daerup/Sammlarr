using System.Text.Json;
using System.Text.RegularExpressions;

namespace Web.Tests.StaticAssets;

public partial class ModuleImportsTests
{
	private static readonly JsonElement Manifest = JsonDocument
		.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Web.staticwebassets.runtime.json")))
		.RootElement;

	[Test]
	public async Task Modules_ImportOnlyModulesThatAreServed_SoTrimmingAnAssetCannotBreakAnother()
	{
		var roots = Manifest.GetProperty("ContentRoots").EnumerateArray().Select(root => root.GetString()!).ToList();
		var served = Served(Manifest.GetProperty("Root"), string.Empty).ToDictionary(asset => asset.Route, asset => Path.Combine(roots[asset.Root], asset.SubPath));

		var missing =
			from module in served
			where module.Key.EndsWith(".js", StringComparison.Ordinal) && !module.Key.StartsWith("_framework/", StringComparison.Ordinal)
			from match in RelativeImport().Matches(File.ReadAllText(module.Value))
			let route = new Uri(new Uri($"http://host/{module.Key}"), match.Groups["path"].Value).AbsolutePath[1..]
			where !served.ContainsKey(route)
			select $"{module.Key} imports {route}";

		await Assert.That(missing).IsEmpty();
	}

	private static IEnumerable<(string Route, int Root, string SubPath)> Served(JsonElement node, string route)
	{
		if (node.GetProperty("Asset") is { ValueKind: JsonValueKind.Object } asset)
		{
			yield return (route, asset.GetProperty("ContentRootIndex").GetInt32(), asset.GetProperty("SubPath").GetString()!);
		}

		if (node.GetProperty("Children") is { ValueKind: JsonValueKind.Object } children)
		{
			foreach (var child in children.EnumerateObject().SelectMany(child => Served(child.Value, route.Length == 0 ? child.Name : $"{route}/{child.Name}")))
			{
				yield return child;
			}
		}
	}

	[GeneratedRegex("""(?:from|import)\s*\(?\s*['"](?<path>\.{1,2}/[^'"]+)['"]""")]
	private static partial Regex RelativeImport();
}
