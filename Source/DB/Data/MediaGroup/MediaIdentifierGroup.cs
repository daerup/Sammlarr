using System.Collections.ObjectModel;

namespace DB.Data.MediaGroup;

internal record MediaIdentifierGroup : Entity<Guid>
{
	public Collection<MediaIdentifierEntry> MediaIdEntries { get; set; } = [];
}
