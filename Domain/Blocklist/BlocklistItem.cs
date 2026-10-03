using Domain.Media;

namespace Domain.Blocklist;

public record BlocklistItem(MediaId MediaId, DateTimeOffset AddedAt) : IListItem<BlocklistedMedia>
{
	public BlocklistedMedia With(MatchedMedia media) => new(media, AddedAt);
}
