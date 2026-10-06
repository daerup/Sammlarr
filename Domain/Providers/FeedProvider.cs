namespace Domain.Providers;

public enum FeedProvider
{
	Mdblist,
}

public static class FeedProviders
{
	extension(FeedProvider feed)
	{
		public Service Service
		{
			get
			{
				return feed switch
				{
					FeedProvider.Mdblist => RemoteService.Mdblist,
				};
			}
		}
	}
}
