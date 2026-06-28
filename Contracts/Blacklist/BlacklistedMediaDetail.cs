namespace Contracts.Blacklist;

public record BlacklistedMediaDetail(MediaMatch Detail, DateTimeOffset AddedAt);
