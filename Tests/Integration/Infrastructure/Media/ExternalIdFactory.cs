using System;
using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

internal static class ExternalIdFactory
{
	public static IExternalMediaId Create(MediaProvider provider, string id, MediaType type) =>
		provider switch
		{
			MediaProvider.Tmdb => TmdbId.From(id, type),
			MediaProvider.Imdb => ImdbId.From(id, type),
			MediaProvider.Tvdb => TvdbId.From(id, type),
			MediaProvider.Plex => PlexId.From(id, type),
			MediaProvider.Mdblist => MdblistId.From(id, type),
			_ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
		};
}
