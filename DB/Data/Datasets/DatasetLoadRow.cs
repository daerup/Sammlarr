using System.ComponentModel.DataAnnotations.Schema;

namespace DB.Data.Datasets;

[Table("DatasetLoads")]
internal sealed record DatasetLoadRow
{
	public required string Name { get; set; }

	public required DateTimeOffset LoadedAt { get; set; }

	public string? ETag { get; set; }

	public required int Rows { get; set; }
}
