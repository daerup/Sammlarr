using System.ComponentModel;
using Domain.Wantlist.Arr;

namespace Domain.Providers;

// The members' order breaks ties when EnricherChoice.Next picks which enricher to ask.
public enum RemoteService
{
	[Description("TMDB")]
	Tmdb,

	[Description("Plex")]
	Plex,

	[Description("MDBList")]
	Mdblist,

	[Description("Wikidata")]
	Wikidata,

	[Description("TVmaze")]
	Tvmaze,

	[Description("IMDb datasets")]
	ImdbDatasets,

	[Description("Radarr")]
	Radarr,

	[Description("Sonarr")]
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
					RemoteService.Wikidata or RemoteService.Tvmaze or RemoteService.ImdbDatasets => new Open(),
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
					RemoteService.Tvmaze => Some.Of(MediaProvider.Tvmaze),
					RemoteService.ImdbDatasets => Some.Of(MediaProvider.Imdb),
					RemoteService.Wikidata or RemoteService.Radarr or RemoteService.Sonarr => new None(),
				};
			}
		}
	}
}
