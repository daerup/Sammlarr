namespace DB.Data.Entities;

public record MediaIdGroup : Entity<Guid>
{
	public List<MediaIdEntry> MediaIds { get; set; } = [];
}
