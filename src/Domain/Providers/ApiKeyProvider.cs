namespace Domain.Providers;

public enum ApiKeyProvider
{
	Tmdb,
	Mdblist,
	TmdbReadToken,
	Mal,
	Trakt,
}

public static class ApiKeyProviders
{
	extension(ApiKeyProvider slot)
	{
		public RemoteService Remote
		{
			get
			{
				return slot switch
				{
					ApiKeyProvider.Tmdb or ApiKeyProvider.TmdbReadToken => RemoteService.Tmdb,
					ApiKeyProvider.Mdblist => RemoteService.Mdblist,
					ApiKeyProvider.Mal => RemoteService.Mal,
					ApiKeyProvider.Trakt => RemoteService.Trakt,
				};
			}
		}
	}
}
