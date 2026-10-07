using System.ComponentModel.DataAnnotations.Schema;
using Domain.Media;

namespace DB.Data.Datasets;

[Table("AnimeIds")]
internal sealed record AnimeIdRow
{
	public long Id { get; init; }

	public required MediaType Type { get; init; }

	public int? AniListId { get; init; }

	public int? MalId { get; init; }

	public int? TvdbId { get; init; }

	public int? TmdbId { get; init; }

	public string? ImdbId { get; init; }
}
