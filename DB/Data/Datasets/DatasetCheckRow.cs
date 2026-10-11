using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Datasets;

namespace DB.Data.Datasets;

[Table("DatasetChecks")]
internal sealed record DatasetCheckRow
{
	public required string Name { get; init; }

	public required DateTimeOffset StartedAt { get; init; }

	public required DateTimeOffset FinishedAt { get; init; }

	public required DatasetOutcome Outcome { get; init; }

	public string? Failure { get; init; }
}
