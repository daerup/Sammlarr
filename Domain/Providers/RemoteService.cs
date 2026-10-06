using Domain.Wantlist.Arr;

namespace Domain.Providers;

public enum RemoteService
{
	Tmdb,
	Plex,
	Mdblist,
	Wikidata,
	Radarr,
	Sonarr,
}

public union Access(Open, UserKey, UserSignIn, AppInstance);

public readonly record struct Open;

public readonly record struct UserKey(ApiKeyProvider Slot);

public readonly record struct UserSignIn(AuthProvider SignIn);

public readonly record struct AppInstance(ArrApp App);

public static class RemoteServices
{
	extension(RemoteService remote)
	{
		public Access Access
		{
			get
			{
				return remote switch
				{
					RemoteService.Tmdb => new UserKey(ApiKeyProvider.Tmdb),
					RemoteService.Mdblist => new UserKey(ApiKeyProvider.Mdblist),
					RemoteService.Plex => new UserSignIn(AuthProvider.Plex),
					RemoteService.Radarr => new AppInstance(ArrApp.Radarr),
					RemoteService.Sonarr => new AppInstance(ArrApp.Sonarr),
					RemoteService.Wikidata => new Open(),
				};
			}
		}

		public Optional<MediaProvider> OwnIds
		{
			get
			{
				return remote switch
				{
					RemoteService.Tmdb => Some.Of(MediaProvider.Tmdb),
					RemoteService.Plex => Some.Of(MediaProvider.Plex),
					RemoteService.Mdblist => Some.Of(MediaProvider.Mdblist),
					RemoteService.Wikidata or RemoteService.Radarr or RemoteService.Sonarr => new None(),
				};
			}
		}
	}
}
