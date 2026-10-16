using Domain.Media;

namespace Contracts.Datasets;

public interface IImdbRatings : IDatasetTable<ImdbRatingLine>
{
	Task<Optional<ImdbRating>> FindAsync(string tconst, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<string, ImdbRating>> FindManyAsync(IReadOnlyCollection<string> tconsts, CancellationToken cancellationToken = default);
}

public readonly record struct ImdbRatingLine(string Tconst, float Average, int Votes);
