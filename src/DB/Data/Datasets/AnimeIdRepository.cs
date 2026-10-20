using System.Data;
using System.Data.Common;
using System.Globalization;
using Contracts.Datasets;
using Domain.Media;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DB.Data.Datasets;

internal sealed class AnimeIdRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IAnimeIds
{
	private const int SliceSize = 1000;

	private const string Insert =
		"INSERT INTO AnimeIds (Type, AniListId, MalId, TvdbId, TmdbId, ImdbId, SeriesTvdbId, TvdbSeason, EpisodeOffset) VALUES ($type, $anilist, $mal, $tvdb, $tmdb, $imdb, $series, $season, $offset)";

	public async Task<Optional<AnimeIdMapping>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		var isNumber = int.TryParse(id.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number);
		if (!isNumber && id.Source is not MediaProvider.Imdb)
		{
			return new None();
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = ctx.AnimeIds.AsNoTracking().OrderBy(entry => entry.Id);
		var typed = rows.Where(entry => id.Type == MediaType.Unknown || entry.Type == id.Type);
		var row = id.Source switch
		{
			MediaProvider.AniList => await rows.FirstOrDefaultAsync(entry => entry.AniListId == number, cancellationToken),
			MediaProvider.Mal => await rows.FirstOrDefaultAsync(entry => entry.MalId == number, cancellationToken),
			MediaProvider.Tvdb => await typed.FirstOrDefaultAsync(entry => entry.TvdbId == number, cancellationToken),
			MediaProvider.Tmdb => await typed.FirstOrDefaultAsync(entry => entry.TmdbId == number, cancellationToken),
			MediaProvider.Imdb => await typed.FirstOrDefaultAsync(entry => entry.ImdbId == id.Value, cancellationToken),
			MediaProvider.RottenTomatoes
			or MediaProvider.Plex
			or MediaProvider.Mdblist
			or MediaProvider.Metacritic
			or MediaProvider.Letterboxd
			or MediaProvider.Trakt
			or MediaProvider.Tvmaze => null,
		};
		return Optional.Of(row).Map(Entry);
	}

	public async Task<IReadOnlyDictionary<IExternalMediaId, AnimeIdMapping>> FindManyAsync(
		IReadOnlyCollection<IExternalMediaId> ids,
		CancellationToken cancellationToken = default
	)
	{
		List<IExternalMediaId> asked = [.. ids.Distinct()];
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		List<AnimeIdRow> rows = [];
		foreach (var slice in asked.Chunk(SliceSize))
		{
			rows.AddRange(await RowsAsync(ctx, slice, cancellationToken));
		}

		var columns = new Columns([.. rows.DistinctBy(row => row.Id).OrderBy(row => row.Id)]);
		return asked.Select(id => columns.Find(id).Map(row => (Id: id, Entry: Entry(row)))).WhereSome().ToDictionary(found => found.Id, found => found.Entry);
	}

	private static Task<List<AnimeIdRow>> RowsAsync(SammlarrDbContext ctx, IReadOnlyCollection<IExternalMediaId> ids, CancellationToken cancellationToken)
	{
		var anilist = Numbers(ids, MediaProvider.AniList);
		var mal = Numbers(ids, MediaProvider.Mal);
		var tvdb = Numbers(ids, MediaProvider.Tvdb);
		var tmdb = Numbers(ids, MediaProvider.Tmdb);
		List<string?> imdb = [.. ids.Where(id => id.Source is MediaProvider.Imdb).Select(id => id.Value)];
		return ctx
			.AnimeIds.AsNoTracking()
			.Where(entry =>
				anilist.Contains(entry.AniListId)
				|| mal.Contains(entry.MalId)
				|| tvdb.Contains(entry.TvdbId)
				|| tmdb.Contains(entry.TmdbId)
				|| imdb.Contains(entry.ImdbId)
			)
			.ToListAsync(cancellationToken);
	}

	private static List<int?> Numbers(IEnumerable<IExternalMediaId> ids, MediaProvider source)
	{
		return [.. ids.Where(id => id.Source == source).Select(Number).WhereSome().Select(number => (int?)number)];
	}

	private static Optional<int> Number(IExternalMediaId id)
	{
		return Optional.When(int.TryParse(id.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number), () => number);
	}

	public async Task<int> ReplaceAsync(IAsyncEnumerable<AnimeIdMapping> rows, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
		await ctx.AnimeIds.ExecuteDeleteAsync(cancellationToken);
		await using var command = ctx.Database.GetDbConnection().CreateCommand();
		command.Transaction = transaction.GetDbTransaction();
		command.CommandText = Insert;
		var type = Parameter(command, "$type", DbType.String);
		var anilist = Parameter(command, "$anilist", DbType.Int32);
		var mal = Parameter(command, "$mal", DbType.Int32);
		var tvdb = Parameter(command, "$tvdb", DbType.Int32);
		var tmdb = Parameter(command, "$tmdb", DbType.Int32);
		var imdb = Parameter(command, "$imdb", DbType.String);
		var series = Parameter(command, "$series", DbType.Int32);
		var season = Parameter(command, "$season", DbType.Int32);
		var offset = Parameter(command, "$offset", DbType.Int32);
		await command.PrepareAsync(cancellationToken);
		var count = 0;
		await foreach (var row in rows.WithCancellation(cancellationToken))
		{
			type.Value = row.Type.ToString();
			anilist.Value = Value(row.AniList);
			mal.Value = Value(row.Mal);
			tvdb.Value = Value(row.Tvdb);
			tmdb.Value = Value(row.Tmdb);
			imdb.Value = row.Imdb.Map(value => (object)value).GetValueOr(DBNull.Value);
			series.Value = Value(row.SeriesTvdb);
			season.Value = Value(row.TvdbSeason);
			offset.Value = Value(row.EpisodeOffset);
			await command.ExecuteNonQueryAsync(cancellationToken);
			count++;
		}

		if (count == 0)
		{
			throw new InvalidDataException("The anime id map held no entries, so the last one stays.");
		}

		await transaction.CommitAsync(cancellationToken);
		return count;
	}

	private static AnimeIdMapping Entry(AnimeIdRow row)
	{
		return new AnimeIdMapping(
			row.Type,
			Optional.Of(row.AniListId),
			Optional.Of(row.MalId),
			Optional.Of(row.TvdbId),
			Optional.Of(row.TmdbId),
			Optional.Of(row.ImdbId),
			Optional.Of(row.SeriesTvdbId)
		)
		{
			TvdbSeason = Optional.Of(row.TvdbSeason),
			EpisodeOffset = Optional.Of(row.EpisodeOffset),
		};
	}

	private sealed class Columns(IReadOnlyList<AnimeIdRow> rows)
	{
		private readonly ILookup<int?, AnimeIdRow> _aniList = rows.ToLookup(row => row.AniListId);

		private readonly ILookup<int?, AnimeIdRow> _mal = rows.ToLookup(row => row.MalId);

		private readonly ILookup<int?, AnimeIdRow> _tvdb = rows.ToLookup(row => row.TvdbId);

		private readonly ILookup<int?, AnimeIdRow> _tmdb = rows.ToLookup(row => row.TmdbId);

		private readonly ILookup<string?, AnimeIdRow> _imdb = rows.ToLookup(row => row.ImdbId);

		public Optional<AnimeIdRow> Find(IExternalMediaId id)
		{
			return id.Source switch
			{
				MediaProvider.AniList => Numbered(_aniList, id).FirstOrNone(),
				MediaProvider.Mal => Numbered(_mal, id).FirstOrNone(),
				MediaProvider.Tvdb => Numbered(_tvdb, id).FirstOrNone(row => Typed(row, id)),
				MediaProvider.Tmdb => Numbered(_tmdb, id).FirstOrNone(row => Typed(row, id)),
				MediaProvider.Imdb => _imdb[id.Value].FirstOrNone(row => Typed(row, id)),
				MediaProvider.RottenTomatoes
				or MediaProvider.Plex
				or MediaProvider.Mdblist
				or MediaProvider.Metacritic
				or MediaProvider.Letterboxd
				or MediaProvider.Trakt
				or MediaProvider.Tvmaze => new None(),
			};
		}

		private static IEnumerable<AnimeIdRow> Numbered(ILookup<int?, AnimeIdRow> column, IExternalMediaId id)
		{
			return Number(id).Match(number => column[number], Enumerable.Empty<AnimeIdRow>);
		}

		private static bool Typed(AnimeIdRow row, IExternalMediaId id) => id.Type == MediaType.Unknown || row.Type == id.Type;
	}

	private static object Value(Optional<int> value) => value.Map(found => (object)found).GetValueOr(DBNull.Value);

	private static DbParameter Parameter(DbCommand command, string name, DbType type)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = name;
		parameter.DbType = type;
		command.Parameters.Add(parameter);
		return parameter;
	}
}
