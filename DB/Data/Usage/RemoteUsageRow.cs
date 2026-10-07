using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;
using Domain.Usage;

namespace DB.Data.Usage;

[Table("RemoteUsage")]
internal sealed record RemoteUsageRow
{
	public long Id { get; init; }

	public required RemoteService Service { get; init; }

	public required DateTimeOffset Hour { get; init; }

	public required UsageCause Cause { get; init; }

	public required UsageOutcome Outcome { get; init; }

	public Guid? FeedId { get; init; }

	public long Calls { get; set; }
}
