using Contracts.Media;
using Contracts.Media.Metadata;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;

namespace API.Tmdb;

internal class TmdbRatingService(TmdbApi tmdbApi) : IMediaRatingService
{
	public Task<Result<IReadOnlyList<MediaRating>>> GetRatingsAsync(IExternalMediaId id, CancellationToken cancellationToken = default) =>
		id.Require<TmdbId>("TMDB ratings require a TMDB id.")
			.Map(tmdbId => tmdbApi.GetDetailAsync(tmdbId, cancellationToken))
			.When(details => details.VoteAverage is { } voteAverage && voteAverage > 0, "TMDB has no vote data.")
			.Map(details => (IReadOnlyList<MediaRating>)[new MediaRating(RatingProvider.Tmdb, (float)details.VoteAverage.Value, details.VoteCount)]);
}
