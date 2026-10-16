using System.ComponentModel.DataAnnotations.Schema;

namespace DB.Data.Datasets;

[Table("ImdbRatings")]
internal sealed record ImdbRatingRow
{
	public required int Id { get; init; }

	public required byte Score { get; init; }

	public required int Votes { get; init; }
}
