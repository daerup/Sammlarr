namespace Contracts.Media.Blacklist;

public record BlacklistedMedia(IMediaId MediaId, DateTimeOffset AddedAt);
