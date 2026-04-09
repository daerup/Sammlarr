// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using API.Plex;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.TvDB;

internal class TvdbIdConverter(PlexApi plexApi)
{
	public async Task<ResolvedId> ConvertAsync(IMediaId mediaId)
	{
		return mediaId switch
		{
			PlexId plexId => await ResolveById(plexId),
			TmdbId tmdbId => throw new NotSupportedException(),
			ImdbId imdbId => throw new NotSupportedException(),
			TvdbId tvdbId => new ResolvedId(tvdbId, []),
		};
	}

	private async Task<ResolvedId> ResolveById(PlexId plexId)
	{
		var plexResult = await plexApi.ByIdAsync(plexId);
		IMediaId[] otherIds = [plexResult.PlexId, .. plexResult.ImdbIds, .. plexResult.TmdbIds];
		if (plexResult.TvdbIds.Any())
		{
			var tmdbId = plexResult.TvdbIds.First();
			var remainingIds = plexResult.TvdbIds.Skip(1).Cast<IMediaId>();
			return new ResolvedId(tmdbId, remainingIds.Concat(otherIds));
		}

		return ResolvedId.Unresolved(otherIds);
	}
}
