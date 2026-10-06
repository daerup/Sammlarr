namespace Domain.Providers;

public enum AuthProvider
{
	Plex,
	Local,
}

public static class AuthProviders
{
	extension(AuthProvider signIn)
	{
		public Service Service
		{
			get
			{
				return signIn switch
				{
					AuthProvider.Plex => RemoteService.Plex,
					AuthProvider.Local => new SammlarrAccounts(),
				};
			}
		}
	}
}
