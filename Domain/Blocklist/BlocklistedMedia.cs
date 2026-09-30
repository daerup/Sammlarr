using Domain.Media;

namespace Domain.Blocklist;

public record BlocklistedMedia(IdentifiedMedia Media, DateTimeOffset AddedAt);
