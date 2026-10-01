using Domain.Media;

namespace Domain.Wantlist;

public record WantlistItem(MediaId MediaId, DateTimeOffset AddedAt, WantlistItemProvenance Provenance) : IListItem<WantlistedMedia>
{
	public WantlistedMedia With(IdentifiedMedia media) => new(media, AddedAt, Provenance);
}
