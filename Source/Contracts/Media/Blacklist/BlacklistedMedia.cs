namespace Contracts.Media.Blacklist;

public record BlacklistedMedia(MediaId MediaId, DateTimeOffset AddedAt);
