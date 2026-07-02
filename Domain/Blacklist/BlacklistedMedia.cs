using Domain.Media;

namespace Domain.Blacklist;

public record BlacklistedMedia(MediaId MediaId, DateTimeOffset AddedAt);
