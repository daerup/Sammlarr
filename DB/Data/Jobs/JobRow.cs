using Domain.Jobs;

namespace DB.Data.Jobs;

internal record JobRow : Entity<Guid>
{
	public Guid OwnerId { get; set; }

	public JobKind Kind { get; set; }

	public required string Key { get; set; }

	public JobState State { get; set; }

	public DateTimeOffset RunAfter { get; set; }

	public DateTimeOffset FirstArmedAt { get; set; }

	public int Attempts { get; set; }

	public DateTimeOffset? LeaseExpiresAt { get; set; }
}
