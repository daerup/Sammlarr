using Domain.Media;

namespace Domain.Wantlist;

public record WantlistedMedia(MatchedMedia Media, DateTimeOffset AddedAt, WantlistItemProvenance Provenance);
