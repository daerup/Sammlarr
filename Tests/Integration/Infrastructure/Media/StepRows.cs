using Contracts.Media;
using Reqnroll.Assist.Attributes;

namespace Integration.Tests.Infrastructure.Media;

public sealed class MediaDeclarationRow
{
	public IReadOnlyDictionary<MediaProvider, string>? Ids { get; set; }

	[TableAliases("^#$")]
	public string? As { get; set; }
	public string? Type { get; set; }
	public int? Year { get; set; }
}

public sealed class PlexSearchRow
{
	public string? Id { get; set; }
	public string? Title { get; set; }
	public string? Type { get; set; }
}

public sealed class HandleRow
{
	[TableAliases("^#$")]
	public string? Media { get; set; }
	public string? Origin { get; set; }
}

public sealed class MediaIntroRow
{
	[TableAliases("^#$")]
	public string? Key { get; set; }
	public IReadOnlyDictionary<MediaProvider, string>? Ids { get; set; }

	[TableAliases("^with ids of$")]
	public IReadOnlyList<string>? Providers { get; set; }
	public string? Type { get; set; }
	public int? Year { get; set; }
}

public sealed class CachedMediaRow
{
	[TableAliases("^#$")]
	public string? Key { get; set; }
	public IReadOnlyDictionary<MediaProvider, string>? Ids { get; set; }

	[TableAliases("^with ids of$")]
	public IReadOnlyList<string>? Providers { get; set; }
	public string? Type { get; set; }
	public string? Title { get; set; }
	public string? Summary { get; set; }
	public IReadOnlyList<string>? Genres { get; set; }
	public string? Poster { get; set; }
	public string? Fetched { get; set; }
}

public sealed class PlexHoldsRow
{
	[TableAliases("^#$")]
	public string? Key { get; set; }

	[TableAliases("^with ids of$")]
	public IReadOnlyList<string>? Providers { get; set; }
	public string? Title { get; set; }
	public string? Summary { get; set; }
	public IReadOnlyList<string>? Genres { get; set; }
}

public sealed class TmdbHoldsRow
{
	[TableAliases("^#$")]
	public string? Key { get; set; }

	[TableAliases("^with ids of$")]
	public IReadOnlyList<string>? Providers { get; set; }
	public string? Title { get; set; }
	public string? Summary { get; set; }
	public IReadOnlyList<string>? Genres { get; set; }
	public string? VoteAverage { get; set; }
	public int? VoteCount { get; set; }
}

public sealed class OutboxRow
{
	public string? Operation { get; set; }

	[TableAliases("^#$")]
	public string? Key { get; set; }

	public int? Attempts { get; set; }
}

public sealed class DryRunRow
{
	public string? Direction { get; set; }
	public string? Operation { get; set; }

	[TableAliases("^#$")]
	public string? Key { get; set; }
}
