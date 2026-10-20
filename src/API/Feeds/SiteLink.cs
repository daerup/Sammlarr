using Microsoft.AspNetCore.WebUtilities;

namespace API.Feeds;

internal sealed record SiteLink(string Path, IReadOnlyList<KeyValuePair<string, string>> Query)
{
	private static readonly string[] Schemes = ["https://", "http://"];

	internal static Optional<SiteLink> Parse(string input, string host)
	{
		var text = input.Trim().Split('#')[0];
		var scheme = Schemes.FirstOrNone(each => text.StartsWith(each, StringComparison.OrdinalIgnoreCase));
		var rest = scheme.Match(each => text[each.Length..], () => text);
		var site = rest.Split('/', '?')[0];
		var onHost = site.Equals(host, StringComparison.OrdinalIgnoreCase) || site.Equals($"www.{host}", StringComparison.OrdinalIgnoreCase);
		var foreign = !onHost && (scheme.IsSome() || IsHost(site, rest));
		return Optional.When(!foreign, () => Of(onHost ? rest[site.Length..] : rest));
	}

	internal Result<SiteLink> Knowing(IReadOnlySet<string> keys, string site) => Knowing(keys, new HashSet<string>(), site);

	internal Result<SiteLink> Knowing(IReadOnlySet<string> keys, IReadOnlySet<string> dropped, string site)
	{
		var kept = new SiteLink(Path, [.. Query.Where(pair => !dropped.Contains(pair.Key))]);
		return kept
			.Query.Select(pair => pair.Key)
			.FirstOrNone(key => !keys.Contains(key))
			.Match<string, Result<SiteLink>>(
				key => Error.From($"Sammlarr cannot follow {site}'s {key} filter. Remove it from the page, or filter in Sammlarr."),
				() => Success.Of(kept)
			);
	}

	internal IReadOnlyList<string> Values(string key) => [.. Query.Where(pair => pair.Key == key).Select(pair => pair.Value)];

	internal string Stored()
	{
		var query = string.Join("&", Query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
		return query.Length == 0 ? Path : $"{Path}?{query}";
	}

	private static SiteLink Of(string pathAndQuery)
	{
		var parts = pathAndQuery.Split('?', 2);
		List<KeyValuePair<string, string>> pairs = [];
		foreach (var pair in new QueryStringEnumerable(parts.Length == 2 ? parts[1] : string.Empty))
		{
			var key = pair.DecodeName().ToString();
			if (!IsTracking(key))
			{
				pairs.Add(KeyValuePair.Create(key, pair.DecodeValue().ToString()));
			}
		}

		return new SiteLink(parts[0].Trim('/'), pairs);
	}

	private static bool IsHost(string site, string rest) => site.Contains('.', StringComparison.Ordinal) && rest.Length > site.Length && rest[site.Length] == '/';

	private static bool IsTracking(string key) => key == "_location" || key.StartsWith("utm_", StringComparison.Ordinal);
}
