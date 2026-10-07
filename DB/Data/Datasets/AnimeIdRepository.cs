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
	private const string Insert =
		"INSERT INTO AnimeIds (Type, AniListId, MalId, TvdbId, TmdbId, ImdbId, SeriesTvdbId) VALUES ($type, $anilist, $mal, $tvdb, $tmdb, $imdb, $series)";

	public async Task<Optional<AnimeIdMapping>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		if (!int.TryParse(id.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
		{
			return new None();
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = ctx.AnimeIds.AsNoTracking();
		var row = id.Source switch
		{
			MediaProvider.AniList => await rows.FirstOrDefaultAsync(entry => entry.AniListId == number, cancellationToken),
			MediaProvider.Mal => await rows.FirstOrDefaultAsync(entry => entry.MalId == number, cancellationToken),
			MediaProvider.Tvdb => await rows.FirstOrDefaultAsync(entry => entry.TvdbId == number, cancellationToken),
			MediaProvider.Tmdb
			or MediaProvider.Imdb
			or MediaProvider.RottenTomatoes
			or MediaProvider.Plex
			or MediaProvider.Mdblist
			or MediaProvider.Metacritic
			or MediaProvider.Letterboxd
			or MediaProvider.Trakt
			or MediaProvider.Tvmaze => null,
		};
		return Optional.Of(row).Map(Entry);
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
		);
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
