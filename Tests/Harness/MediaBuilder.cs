using Contracts.Blacklist;
using Contracts.Media;
using Contracts.Watchlist;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;

namespace Harness;

internal readonly record struct ProviderId(MediaProvider Provider, string Value)
{
	internal IExternalMediaId ToExternal(MediaType type) =>
		Provider switch
		{
			MediaProvider.Tmdb => TmdbId.From(Value, type),
			MediaProvider.Plex => PlexId.From(Value, type),
			MediaProvider.Imdb => ImdbId.From(Value, type),
			MediaProvider.Tvdb => TvdbId.From(Value, type),
			MediaProvider.Mdblist => MdblistId.From(Value, type),
		};
}

internal static class Ids
{
	internal static ProviderId Tmdb(string id) => new(MediaProvider.Tmdb, id);

	internal static ProviderId Plex(string id) => new(MediaProvider.Plex, id);

	internal static ProviderId Imdb(string id) => new(MediaProvider.Imdb, id);

	internal static ProviderId Tvdb(string id) => new(MediaProvider.Tvdb, id);

	internal static ProviderId Mdblist(string id) => new(MediaProvider.Mdblist, id);
}

internal sealed class MediaBuilder(SammlarrTestHost host, MediaType type, string title, IReadOnlyList<ProviderId> ids)
{
	private const int DefaultYear = 2024;
	private bool _usesPlex;
	private bool _usesTmdb;
	private MediaId? _seeded;

	internal MediaBuilder ServedByPlex(
		IReadOnlyList<ProviderId>? reports = null,
		string? summary = null,
		IReadOnlyList<string>? genres = null,
		string? poster = null,
		string? title = null
	)
	{
		host.PlexHolds(Version(reports, summary, genres, poster, title));
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

	internal async Task<MediaId> OnWatchlist(WatchlistItemProvenance origin)
	{
		var id = await SeedAsync();
		await host.Resolve<IWatchlistRepository>().AddAsync([id], origin);
		return id;
	}

	internal async Task<MediaId> OnBlacklist()
	{
		var id = await SeedAsync();
		await host.Resolve<IBlacklistRepository>().AddAsync(id);
		return id;
	}

	internal async Task<MediaId> SeedWithMetadataAsync(
		string title,
		DateTimeOffset fetched,
		string? summary = null,
		IReadOnlyList<string>? genres = null,
		string? poster = null
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
			DefaultYear,
			title,
			poster,
			summary,
			genres
		);
		_seeded = await host.SeedWithMetadataAsync(resolved, fetched);
		return _seeded.Value;
	}

	private ResolvedVersion Version(IReadOnlyList<ProviderId>? reports, string? summary, IReadOnlyList<string>? genres, string? poster, string? titleOverride)
	{
		var source = reports ?? ids;
		return new ResolvedVersion(
			source.ToDictionary(providerId => providerId.Provider, providerId => providerId.Value),
			type,
			DefaultYear,
			titleOverride ?? title,
			poster,
			summary,
			genres
		);
	}
}
