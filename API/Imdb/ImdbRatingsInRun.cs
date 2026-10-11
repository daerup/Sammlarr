using Contracts.Datasets;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Imdb;

internal sealed class ImdbRatingsInRun(IImdbRatings ratings, IDatasetLoads loads) : IBurstPrimer
{
	private readonly Lock _gate = new();

	private readonly Dictionary<string, Optional<ImdbRating>> _known = [];

	private bool _loaded;

	public RemoteService Remote => RemoteService.ImdbDatasets;

	public async Task PrimeAsync(IReadOnlyCollection<IReadOnlyList<IExternalMediaId>> titles, CancellationToken cancellationToken)
	{
		List<string> asked =
		[
			.. titles.SelectMany(ids => ids).Where(id => id.Source is MediaProvider.Imdb && Known(id.Value).IsNone()).Select(id => id.Value).Distinct(),
		];
		if (asked.Count == 0 || !await LoadedAsync(cancellationToken))
		{
			return;
		}

		var found = await ratings.FindManyAsync(asked, cancellationToken);
		lock (_gate)
		{
			foreach (var tconst in asked)
			{
				_known[tconst] = found.GetValueOrNone(tconst);
			}
		}
	}

	internal async Task<Optional<Optional<ImdbRating>>> FindAsync(string tconst, CancellationToken cancellationToken)
	{
		if (!await LoadedAsync(cancellationToken))
		{
			return new None();
		}

		return Some.Of(await Known(tconst).Match(Task.FromResult, () => ReadAsync(tconst, cancellationToken)));
	}

	private async Task<Optional<ImdbRating>> ReadAsync(string tconst, CancellationToken cancellationToken)
	{
		var rating = await ratings.FindAsync(tconst, cancellationToken);
		lock (_gate)
		{
			_known[tconst] = rating;
		}

		return rating;
	}

	private Optional<Optional<ImdbRating>> Known(string tconst)
	{
		lock (_gate)
		{
			return _known.GetValueOrNone(tconst);
		}
	}

	private async Task<bool> LoadedAsync(CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _loaded))
		{
			return true;
		}

		var loaded = (await loads.FindAsync(DatasetNames.ImdbRatings, cancellationToken)).IsSome();
		if (loaded)
		{
			Volatile.Write(ref _loaded, true);
		}

		return loaded;
	}
}
