using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;

namespace DB.Data.Media;

[Table("ShowParts")]
internal sealed record ShowPartRow
{
	public required MediaProvider Source { get; init; }

	public required string Id { get; init; }

	public MediaRow? Media { get; set; }

	public int? TvdbSeason { get; init; }

	public int? EpisodeOffset { get; init; }

	public required DateTimeOffset AskedAt { get; init; }
}
