using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;
using Domain.Usage;

namespace DB.Data.Usage;

[Table("RemoteAttempts")]
internal sealed record RemoteAttemptRow
{
	public long Id { get; init; }

	public required RemoteService Service { get; init; }

	public required DateTimeOffset Hour { get; init; }

	public required UsageCause Cause { get; init; }

	public Guid? FeedId { get; init; }

	public long Attempts { get; set; }

	public List<RemoteAttemptBucketRow> Buckets { get; init; } = [];
}
