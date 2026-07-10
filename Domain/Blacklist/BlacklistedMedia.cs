using Domain.Media;

namespace Domain.Blacklist;

public record BlacklistedMedia(IdentifiedMedia Media, DateTimeOffset AddedAt);
