using Contracts.Datasets;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Anime;

internal sealed class AnimeIdsInRun(IAnimeIds map, IDatasetLoads loads) : IBurstPrimer
{
	private static readonly MediaProvider[] Mapped = [MediaProvider.AniList, MediaProvider.Mal, MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb];

	private readonly Lock _gate = new();

	private readonly Dictionary<IExternalMediaId, Optional<AnimeIdMapping>> _known = [];

	private bool _loaded;

	public RemoteService Remote => RemoteService.AnimeLists;

	public Task PrimeAsync(IReadOnlyCollection<IReadOnlyList<IExternalMediaId>> titles, CancellationToken cancellationToken)
	{
		return PrimeIdsAsync([.. titles.SelectMany(ids => ids)], cancellationToken);
	}

	internal async Task PrimeIdsAsync(IReadOnlyCollection<IExternalMediaId> ids, CancellationToken cancellationToken)
	{
		List<IExternalMediaId> asked = [.. ids.Where(id => Mapped.Contains(id.Source) && Known(id).IsNone()).Distinct()];
		if (asked.Count == 0 || !await LoadedAsync(cancellationToken))
		{
			return;
		}

		var entries = await map.FindManyAsync(asked, cancellationToken);
		lock (_gate)
		{
			foreach (var id in asked)
			{
				_known[id] = entries.GetValueOrNone(id);
			}
		}
	}

	internal async Task<Result<Optional<AnimeIdMapping>>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		if (!await LoadedAsync(cancellationToken))
		{
			return AnimeListsIdConverter.NotLoaded;
		}

		return Success.Of(await Known(id).GetValueOr(() => ReadAsync(id, cancellationToken)));
	}

	private async Task<Optional<AnimeIdMapping>> ReadAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		var entry = await map.FindAsync(id, cancellationToken);
		lock (_gate)
		{
			_known[id] = entry;
		}

		return entry;
	}

	private Optional<Optional<AnimeIdMapping>> Known(IExternalMediaId id)
	{
		lock (_gate)
		{
			return _known.GetValueOrNone(id);
		}
	}

	private async Task<bool> LoadedAsync(CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _loaded))
		{
			return true;
		}

		var loaded = (await loads.FindAsync(DatasetNames.AnimeIds, cancellationToken)).IsSome();
		if (loaded)
		{
			Volatile.Write(ref _loaded, true);
		}

		return loaded;
	}
}
