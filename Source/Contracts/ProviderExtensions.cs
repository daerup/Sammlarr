using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.List;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;

namespace Contracts;

public static class ProviderExtensions
{
	extension(WatchlistProvider provider)
	{
		public MediaProvider AsMediaProvider() =>
			provider switch
			{
				WatchlistProvider.Plex => MediaProvider.Plex,
			};
	}

	extension(MediaSearchProvider provider)
	{
		public MediaProvider AsMediaProvider() =>
			provider switch
			{
				MediaSearchProvider.Plex => MediaProvider.Plex,
			};
	}

	extension(MediaRecommendationProvider provider)
	{
		public MediaProvider AsMediaProvider() =>
			provider switch
			{
				MediaRecommendationProvider.Tmdb => MediaProvider.Tmdb,
			};
	}

	extension(MediaListProvider provider)
	{
		public MediaProvider AsMediaProvider() =>
			provider switch
			{
				MediaListProvider.Mdblist => MediaProvider.Mdblist,
				MediaListProvider.Tmdb => MediaProvider.Tmdb,
			};
	}

	extension(RatingProvider provider)
	{
		public MediaProvider AsMediaProvider() =>
			provider switch
			{
				RatingProvider.Tmdb => MediaProvider.Tmdb,
			};

		public RatingSource AsRatingSource() =>
			provider switch
			{
				RatingProvider.Tmdb => RatingSource.Tmdb,
			};
	}

	extension(MediaProvider provider)
	{
		public RatingSource? AsRatingSource() =>
			provider switch
			{
				MediaProvider.Tmdb => RatingSource.Tmdb,
				_ => null,
			};
	}
}
