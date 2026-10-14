using DB.Data.Media;

namespace DB.Data.Blocklist;

internal sealed record BlocklistRow : IAuditable, IUserOwned
{
	public required MediaRow Media { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
