namespace Domain.Providers;

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

		public ApiKeyProvider AsApiKeyProvider() =>
			provider switch
			{
				MediaRecommendationProvider.Tmdb => ApiKeyProvider.Tmdb,
			};
	}

	extension(FeedProvider provider)
	{
		public MediaProvider AsMediaProvider() =>
			provider switch
			{
				FeedProvider.Mdblist => MediaProvider.Mdblist,
				FeedProvider.Tmdb => MediaProvider.Tmdb,
			};

		public ApiKeyProvider AsApiKeyProvider() =>
			provider switch
			{
				FeedProvider.Mdblist => ApiKeyProvider.Mdblist,
				FeedProvider.Tmdb => ApiKeyProvider.Tmdb,
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

	extension(ApiKeyProvider provider)
	{
		public MediaProvider AsMediaProvider() =>
			provider switch
			{
				ApiKeyProvider.Tmdb => MediaProvider.Tmdb,
				ApiKeyProvider.Mdblist => MediaProvider.Mdblist,
			};

		public RatingSource? AsRatingSource() =>
			provider switch
			{
				ApiKeyProvider.Tmdb => RatingSource.Tmdb,
				_ => null,
			};
	}
}
