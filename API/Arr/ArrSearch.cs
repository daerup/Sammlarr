using API.Arr.Types;
using Contracts.Media.Search;
using Contracts.Settings;
using Domain.Media;
using Domain.Wantlist.Arr;
using Microsoft.Extensions.Logging;

namespace API.Arr;

internal sealed class ArrSearch(ArrClients clients, IArrSinkConfigService configs, ILogger<ArrSearch> logger) : IMediaSearchProvider
{
	public async Task<IReadOnlyList<FoundMedia>> SearchAsync(
		string query,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var config = await configs.GetAsync(cancellationToken);
		var apps = Enum.GetValues<ArrApp>().Where(app => mediaTypes.Contains(app.Type));
		var answers = await Task.WhenAll(apps.Select(app => FoundAsync(app, config, query, cancellationToken)));
		if (answers.Length > 0 && answers.All(answer => answer.IsError()))
		{
			throw new InvalidOperationException(string.Join(" ", answers.Select(answer => answer.Match(_ => string.Empty, error => error.Reason))));
		}

		return [.. answers.SelectMany(answer => answer.GetValueOr([]))];
	}

	public async Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var config = await configs.GetAsync(cancellationToken);
		return await AppFor(mediaId)
			.ToResult("Radarr looks movies up by their TMDB id and Sonarr shows by their TVDB id.")
			.Map(app => FindAsync(app, config, mediaId, cancellationToken));
	}

	private async Task<Result<List<FoundMedia>>> FoundAsync(ArrApp app, ArrSinkConfig config, string query, CancellationToken cancellationToken)
	{
		var titles = await SearchAsync(app, config, query, cancellationToken);
		return titles
			.ApplyError(error => logger.LogWarning("{App} search for {Query} failed: {Reason}", app.Name, query, error.Reason))
			.Map(found => found.Select(title => ArrFound.Of(app, title)).ToList());
	}

	private Task<Result<IReadOnlyList<ArrTitle>>> SearchAsync(ArrApp app, ArrSinkConfig config, string term, CancellationToken cancellationToken)
	{
		return ArrSession
			.Connect(app, config.Of(app))
			.Match(connection => clients.For(app).SearchAsync(connection, term, cancellationToken), error => Task.FromResult<Result<IReadOnlyList<ArrTitle>>>(error));
	}

	private async Task<Result<FoundMedia>> FindAsync(ArrApp app, ArrSinkConfig config, IExternalMediaId mediaId, CancellationToken cancellationToken)
	{
		var titles = await SearchAsync(app, config, $"{app.LookupTerm}:{mediaId.Value}", cancellationToken);
		return titles.Map(found => found.FirstOrNone().Map(title => ArrFound.Of(app, title)).ToResult($"{app.Name} has no title for {mediaId.Value}."));
	}

	private static Optional<ArrApp> AppFor(IExternalMediaId mediaId)
	{
		return mediaId switch
		{
			TmdbId { Type: MediaType.Movie } => Some.Of(ArrApp.Radarr),
			TvdbId { Type: MediaType.Show } => Some.Of(ArrApp.Sonarr),
			_ => new None(),
		};
	}
}
