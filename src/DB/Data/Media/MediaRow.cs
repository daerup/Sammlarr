using System.Collections.ObjectModel;

namespace DB.Data.Media;

internal sealed record MediaRow : Entity<Guid>
{
	public Collection<ExternalMediaIdRow> ExternalIds { get; set; } = [];

	public MediaDetailsRow? Details { get; set; }

	public DateTimeOffset? DetailsTrimmedAt { get; set; }
}
