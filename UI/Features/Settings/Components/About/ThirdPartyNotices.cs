using System.Text.Json;

namespace UI.Features.Settings.Components.About;

internal sealed record NoticedPackage(string Id, string Version, string Project, string Copyright);

internal sealed record NoticedLicence(string Id, string Text, IReadOnlyList<NoticedPackage> Packages);

internal sealed record Notices(IReadOnlyList<NoticedLicence> Licences);

internal sealed class ThirdPartyNotices(string directory)
{
	private const string FileName = "third-party-notices.json";

	public async Task<Optional<Notices>> ReadAsync()
	{
		var path = Path.Combine(directory, FileName);
		if (!File.Exists(path))
		{
			return new None();
		}

		await using var json = File.OpenRead(path);
		return Some.Of((await JsonSerializer.DeserializeAsync<Notices>(json, JsonSerializerOptions.Web))!);
	}
}
