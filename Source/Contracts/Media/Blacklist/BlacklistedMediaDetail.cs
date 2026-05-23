using Contracts.Media.Search;

namespace Contracts.Media.Blacklist;

public record BlacklistedMediaDetail(IMediaDetailResult Detail, DateTimeOffset AddedAt);
