using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Housekeeping;

namespace DB.Data.Housekeeping;

[Table("HousekeepingReportSteps")]
internal sealed record HousekeepingStepRow
{
	public int ReportId { get; init; }

	public HousekeepingStep Step { get; init; }

	public int Removed { get; init; }
}
