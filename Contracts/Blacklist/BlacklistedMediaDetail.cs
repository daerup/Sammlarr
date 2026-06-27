namespace Contracts.Blacklist;

public record BlacklistedMediaDetail(MediaLookup Detail, DateTimeOffset AddedAt);
