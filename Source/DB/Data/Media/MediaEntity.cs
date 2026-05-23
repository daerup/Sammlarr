using System.Collections.ObjectModel;

namespace DB.Data.Media;

internal record MediaEntity : Entity<Guid>
{
	public Collection<ExternalMediaIdEntity> ExternalIds { get; set; } = [];
}
