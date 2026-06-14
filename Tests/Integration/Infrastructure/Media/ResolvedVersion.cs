using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

public sealed record ResolvedVersion(
	IReadOnlyDictionary<MediaProvider, string> Ids,
	MediaType Type,
	int Year,
	string Title,
	string? Poster,
	string? Summary,
	IReadOnlyList<string>? Genres
);
