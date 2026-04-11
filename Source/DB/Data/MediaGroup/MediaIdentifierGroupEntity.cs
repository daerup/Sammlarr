using System.Collections.ObjectModel;

namespace DB.Data.MediaGroup;

internal record MediaIdentifierGroupEntity : Entity<Guid>
{
	public Collection<MediaIdentifierGroupEntry> MediaIdEntries { get; set; } = [];
}
