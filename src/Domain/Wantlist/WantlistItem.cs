using Domain.Media;

namespace Domain.Wantlist;

public record WantlistItem(MediaId MediaId, DateTimeOffset AddedAt, WantlistItemProvenance Provenance) : IListItem<WantlistedMedia>
{
	public WantlistedMedia With(MatchedMedia media) => new(media, AddedAt, Provenance);
}
