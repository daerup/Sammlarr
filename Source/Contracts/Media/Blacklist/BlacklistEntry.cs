namespace Contracts.Media.Blacklist;

public record BlacklistEntry(IMediaId Id, DateTimeOffset AddedDate);

