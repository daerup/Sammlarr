using System.Collections.ObjectModel;

namespace DB.Data.Entities;

public record MediaIdGroup : Entity<Guid>
{
	public Collection<MediaIdEntry> MediaIds { get; set; } = [];
}
