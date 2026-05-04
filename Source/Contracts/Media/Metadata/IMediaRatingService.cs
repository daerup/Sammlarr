namespace Contracts.Media.Metadata;

public interface IMediaRatingService
{
	Task<IReadOnlyList<MediaRating>> GetRatingsAsync(IMediaId id, CancellationToken cancellationToken = default);
}
