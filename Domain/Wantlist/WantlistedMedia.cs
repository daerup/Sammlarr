using Domain.Media;

namespace Domain.Wantlist;

public record WantlistedMedia(IdentifiedMedia Media, DateTimeOffset AddedAt, WantlistItemProvenance Provenance);
