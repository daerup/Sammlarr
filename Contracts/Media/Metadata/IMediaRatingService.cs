using Domain.Media;
using Domain.Media.Metadata;

namespace Contracts.Media.Metadata;

public interface IMediaRatingService
{
	Task<Result<IReadOnlyList<MediaRating>>> GetRatingsAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
