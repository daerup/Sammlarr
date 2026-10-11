using API.Arr.Types;
using Contracts.Media.Search;
using Contracts.Settings;
using Domain.Media;
using Domain.Wantlist.Arr;
using Microsoft.Extensions.Logging;

namespace API.Arr;

internal sealed class ArrSearch(ArrApis apis, IArrSinkConfigService configs, ILogger<ArrSearch> logger) : IMediaSearchProvider
{
	public async Task<IReadOnlyList<FoundMedia>> SearchAsync(
		string query,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var config = await configs.GetAsync(cancellationToken);
		var asked = apis.All.Where(api => mediaTypes.Contains(api.App.Type));
		var answers = await Task.WhenAll(asked.Select(api => FoundAsync(api, config, query, cancellationToken)));
		if (answers.Length > 0 && answers.All(answer => answer.IsError()))
		{
			throw new InvalidOperationException(string.Join(" ", answers.Select(answer => answer.Match(_ => string.Empty, error => error.Reason))));
		}

		return [.. answers.SelectMany(answer => answer.GetValueOr([]))];
	}

	private async Task<Result<List<FoundMedia>>> FoundAsync(ArrApi api, ArrSinkConfig config, string query, CancellationToken cancellationToken)
	{
		var titles = await SearchAsync(api, config, query, cancellationToken);
		return titles
			.ApplyError(error => logger.LogWarning("{App} search for {Query} failed: {Reason}", api.App.Name, query, error.Reason))
			.Map(found => found.Select(title => ArrFound.Of(api, title)).ToList());
	}

	private static Task<Result<IReadOnlyList<ArrTitle>>> SearchAsync(ArrApi api, ArrSinkConfig config, string term, CancellationToken cancellationToken)
	{
		return ArrSession.Connect(api.App, config.Of(api.App)).Map(connection => api.SearchAsync(connection, term, cancellationToken));
	}
}
