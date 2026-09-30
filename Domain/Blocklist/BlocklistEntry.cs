using Domain.Media;

namespace Domain.Blocklist;

public record BlocklistEntry(MediaId MediaId, DateTimeOffset AddedAt);
