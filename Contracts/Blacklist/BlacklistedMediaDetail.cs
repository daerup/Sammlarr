using Contracts.Media.Search;

namespace Contracts.Blacklist;

public record BlacklistedMediaDetail(IMediaDetail Detail, DateTimeOffset AddedAt);
