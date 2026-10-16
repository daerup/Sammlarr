using Domain.Media;
using Domain.Providers;

namespace Contracts.Media.Search;

public interface IMediaSearchProvider
{
	Task<SearchAnswer> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);
}

public sealed record SearchAnswer(IReadOnlyList<FoundMedia> Found, IReadOnlyList<SearchMiss> Missed);

public sealed record SearchMiss(RemoteService Service, string Reason);
