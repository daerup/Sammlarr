using Domain.Jobs;

namespace DB.Data.Jobs;

internal sealed record JobRow : Entity<Guid>
{
	public Guid OwnerId { get; set; }

	public JobKind Kind { get; set; }

	public required string Key { get; set; }

	public required IJobRequest Request { get; set; }

	public JobState State { get; set; }

	public DateTimeOffset RunAfter { get; set; }

	public DateTimeOffset Deadline { get; set; }

	public int Attempts { get; set; }
}
