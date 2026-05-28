namespace Contracts.Media.Metadata;

public interface IMediaRatingService
{
	Task<IReadOnlyList<MediaRating>> GetRatingsAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
