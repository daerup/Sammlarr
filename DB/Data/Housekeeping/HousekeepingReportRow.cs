using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Housekeeping;

namespace DB.Data.Housekeeping;

[Table("HousekeepingReports")]
internal sealed record HousekeepingReportRow
{
	public int Id { get; init; }

	public DateTimeOffset StartedAt { get; init; }

	public DateTimeOffset? FinishedAt { get; init; }

	public long? BytesBefore { get; init; }

	public long? BytesAfter { get; init; }

	public bool? Vacuumed { get; init; }

	public HousekeepingStep? FailedStep { get; init; }

	public string? FailureReason { get; init; }

	public List<HousekeepingStepRow> Steps { get; init; } = [];
}
