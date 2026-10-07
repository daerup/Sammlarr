using System.ComponentModel.DataAnnotations.Schema;

namespace DB.Data.Datasets;

[Table("ImdbRatings")]
internal sealed record ImdbRatingRow
{
	public required string Tconst { get; init; }

	public required float Average { get; init; }

	public required int Votes { get; init; }
}
