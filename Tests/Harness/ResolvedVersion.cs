using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace Harness;

public sealed record ResolvedVersion(
	IReadOnlyDictionary<MediaProvider, string> Ids,
	MediaType Type,
	int Year,
	string Title,
	string? Poster,
	string? Summary,
	IReadOnlyList<string>? Genres
);
