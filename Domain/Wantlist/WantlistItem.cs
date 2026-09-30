using Domain.Media;

namespace Domain.Wantlist;

public record WantlistItem(MediaId MediaId, DateTimeOffset AddedAt, WantlistItemProvenance Provenance);
