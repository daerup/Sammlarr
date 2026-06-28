using DB.Data.Media;

namespace DB.Data.Blacklist;

internal record BlacklistEntity : IAuditable
{
	public required MediaRow Media { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
