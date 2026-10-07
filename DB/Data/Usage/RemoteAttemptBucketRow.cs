using System.ComponentModel.DataAnnotations.Schema;

namespace DB.Data.Usage;

[Table("RemoteAttemptBuckets")]
internal sealed record RemoteAttemptBucketRow
{
	public required int UpToMs { get; init; }

	public long Count { get; set; }
}
