namespace Domain.Providers;

public enum ExternalIdRole
{
	IdentityKey,
	PageLink,
}

public static class MediaProviderRoles
{
	extension(MediaProvider provider)
	{
		public ExternalIdRole Role
		{
			get
			{
				return provider switch
				{
					MediaProvider.Tmdb
					or MediaProvider.Imdb
					or MediaProvider.Tvdb
					or MediaProvider.Plex
					or MediaProvider.Mdblist
					or MediaProvider.Trakt
					or MediaProvider.Mal
					or MediaProvider.Tvmaze
					or MediaProvider.AniList => ExternalIdRole.IdentityKey,
					MediaProvider.RottenTomatoes or MediaProvider.Metacritic or MediaProvider.Letterboxd => ExternalIdRole.PageLink,
				};
			}
		}
	}
}
