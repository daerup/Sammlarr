namespace Domain.Providers;

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
					ApiKeyProvider.Tmdb => RemoteService.Tmdb,
					ApiKeyProvider.Mdblist => RemoteService.Mdblist,
				};
			}
		}
	}
}
