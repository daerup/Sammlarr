namespace Domain.Providers;

public enum SearchProvider
{
	Tmdb,
	Arr,
	Plex,
}

public static class SearchProviders
{
	extension(SearchProvider search)
	{
		public Service Service
		{
			get
			{
				return search switch
				{
					SearchProvider.Tmdb => RemoteService.Tmdb,
					SearchProvider.Plex => RemoteService.Plex,
					SearchProvider.Arr => new Arr(),
				};
			}
		}
	}
}
