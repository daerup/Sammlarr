using Domain.Media;

namespace Contracts.Media.Search;

public interface IMediaSearchService
{
	Task<SearchResults> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);
}

public sealed record SearchResults(IReadOnlyList<MediaMatchResult> Results, IReadOnlyList<SearchMiss> Missed);
