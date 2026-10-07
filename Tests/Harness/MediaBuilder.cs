using Contracts.Blocklist;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;

namespace Harness;

internal readonly record struct ProviderId(MediaProvider Provider, string Value)
{
	internal IExternalMediaId ToExternal(MediaType type) =>
		Provider switch
		{
			MediaProvider.Tmdb => TmdbId.From(Value, type),
			MediaProvider.Plex => PlexId.From(Value, type),
			MediaProvider.Imdb => ImdbId.From(Value, type),
			MediaProvider.RottenTomatoes => RottenTomatoesId.From(Value, type),
			MediaProvider.Tvdb => TvdbId.From(Value, type),
			MediaProvider.Mdblist => MdblistId.From(Value, type),
			MediaProvider.Metacritic => MetacriticId.From(Value, type),
			MediaProvider.Letterboxd => LetterboxdId.From(Value, type),
			MediaProvider.Trakt => TraktId.From(Value, type),
			MediaProvider.Mal => MalId.From(Value, type),
			MediaProvider.Tvmaze => TvmazeId.From(Value, type),
		};
}

internal static class Ids
{
	internal static ProviderId Tmdb(string id) => new(MediaProvider.Tmdb, id);

	internal static ProviderId Plex(string id) => new(MediaProvider.Plex, id);

	internal static ProviderId Imdb(string id) => new(MediaProvider.Imdb, id);

	internal static ProviderId Tvdb(string id) => new(MediaProvider.Tvdb, id);
}

internal sealed class MediaBuilder(SammlarrTestHost host, MediaType type, string title, IReadOnlyList<ProviderId> ids)
{
	private bool _usesPlex;
	private bool _usesTmdb;
	private MediaId? _seeded;

	internal MediaBuilder ServedByPlex(
		IReadOnlyList<ProviderId>? reports = null,
		string? summary = null,
		IReadOnlyList<string>? genres = null,
		string? poster = null,
		string? title = null,
		double? imdbRating = null,
		int imdbVotes = 0
	)
	{
		host.PlexHolds(Version(reports, summary, genres, poster, title), imdbRating, imdbVotes);
		_usesPlex = true;
		return this;
	}

	internal MediaBuilder ServedByTmdb(
		IReadOnlyList<ProviderId>? reports = null,
		double? rating = null,
		int votes = 0,
		string? summary = null,
		IReadOnlyList<string>? genres = null,
		string? title = null
	)
	{
		host.TmdbHolds(Version(reports, summary, genres, null, title), rating, votes);
		_usesTmdb = true;
		return this;
	}

	internal async Task<MediaId> SeedAsync()
	{
		if (_seeded is { } existing)
		{
			return existing;
		}

		if (_usesPlex)
		{
			await host.EnsurePlexAccount();
		}

		if (_usesTmdb)
		{
			await host.EnsureTmdbAccount();
		}

		var id = await host.SeedMediaAsync([.. ids.Select(providerId => providerId.ToExternal(type))]);
		_seeded = id;
		return id;
	}

	internal async Task<MediaId> OnWantlist(WantlistItemProvenance origin)
	{
		var id = await SeedAsync();
		await host.Resolve<IWantlistRepository>().AddAsync([id], origin);
		return id;
	}

	internal async Task<MediaId> OnBlocklist()
	{
		var id = await SeedAsync();
		await host.Resolve<IBlocklistRepository>().AddAsync([id]);
		return id;
	}

	internal async Task<MediaId> SeedWithDetailsAsync(
		string title,
		DateTimeOffset fetched,
		string? summary = null,
		IReadOnlyList<string>? genres = null,
		string? poster = null,
		IReadOnlyCollection<RemoteService>? answeredBy = null
	)
	{
		if (_usesPlex)
		{
			await host.EnsurePlexAccount();
		}

		if (_usesTmdb)
		{
			await host.EnsureTmdbAccount();
		}

		var resolved = new ResolvedVersion(
			ids.ToDictionary(providerId => providerId.Provider, providerId => providerId.Value),
			type,
			SammlarrTestHost.DefaultYear,
			title,
			poster,
			summary,
			genres
		);
		_seeded = await host.SeedWithDetailsAsync(resolved, fetched, answeredBy ?? host.Enrichers);
		return _seeded.Value;
	}

	private ResolvedVersion Version(IReadOnlyList<ProviderId>? reports, string? summary, IReadOnlyList<string>? genres, string? poster, string? titleOverride)
	{
		var source = reports ?? ids;
		return new ResolvedVersion(
			source.ToDictionary(providerId => providerId.Provider, providerId => providerId.Value),
			type,
			SammlarrTestHost.DefaultYear,
			titleOverride ?? title,
			poster,
			summary,
			genres
		);
	}
}
