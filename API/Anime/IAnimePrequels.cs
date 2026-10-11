namespace API.Anime;

internal interface IAnimePrequels
{
	Task<IReadOnlyDictionary<int, Result<Optional<int>>>> PrequelsOfAsync(IReadOnlyList<int> seasons, CancellationToken cancellationToken = default);
}
