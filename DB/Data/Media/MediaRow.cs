using System.Collections.ObjectModel;

namespace DB.Data.Media;

internal record MediaRow : Entity<Guid>
{
	public Collection<ExternalMediaIdRow> ExternalIds { get; set; } = [];
}
