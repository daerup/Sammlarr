using Contracts.Media.Search;

namespace Contracts.Media.Blacklist;

public record BlacklistedItem(IMediaDetailResult Detail, DateTimeOffset AddedDate);
