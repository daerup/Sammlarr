using Domain.Media;

namespace Domain.Blacklist;

public record BlacklistEntry(MediaId MediaId, DateTimeOffset AddedAt);
