using Contracts.Media;

namespace Contracts.Blacklist;

public record BlacklistedMedia(MediaId MediaId, DateTimeOffset AddedAt);
