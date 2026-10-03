using Domain.Media;

namespace Domain.Blocklist;

public record BlocklistedMedia(MatchedMedia Media, DateTimeOffset AddedAt);
