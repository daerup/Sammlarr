using System.Collections.ObjectModel;

namespace DB.Data.Entities;

public record MediaIdentifierGroup : Entity<Guid>
{
	public Collection<MediaIdentifierEntry> MediaIdEntries { get; set; } = [];
}

