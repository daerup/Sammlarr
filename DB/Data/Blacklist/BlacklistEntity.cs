using DB.Data.Media;

namespace DB.Data.Blacklist;

internal record BlacklistEntity : IAuditable
{
	public required MediaEntity Media { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
