using System.Collections.ObjectModel;
using DB.Data.Media.Metadata;

namespace DB.Data.Media;

internal record MediaRow : Entity<Guid>
{
	public Collection<ExternalMediaIdRow> ExternalIds { get; set; } = [];

	public MediaMetadataRow? Metadata { get; set; }
}
