using Contracts.Media;
using Contracts.Media.Metadata;
using Domain.Media;
using Domain.Media.Metadata;

namespace API.Tmdb;

internal class TmdbRatingService(TmdbApi tmdbApi) : IMediaRatingService
{
	public async Task<IReadOnlyList<MediaRating>> GetRatingsAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		if (id is not TmdbId tmdbId)
		{
			return [];
		}

		try
		{
			var details = await tmdbApi.GetDetailAsync(tmdbId, cancellationToken);
			if (details?.VoteAverage is not { } voteAverage || voteAverage <= 0)
			{
				return [];
			}

			return [new MediaRating(RatingSource.Tmdb, (float)voteAverage, details.VoteCount)];
		}
		catch (InvalidOperationException)
		{
			return [];
		}
	}
}
