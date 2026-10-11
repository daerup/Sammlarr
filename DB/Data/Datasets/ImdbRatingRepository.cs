using System.Data;
using System.Data.Common;
using Contracts.Datasets;
using Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DB.Data.Datasets;

internal sealed class ImdbRatingRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IImdbRatings
{
	private const int SliceSize = 1000;

	private const string Insert = "INSERT INTO ImdbRatings (Tconst, Average, Votes) VALUES ($tconst, $average, $votes)";

	public async Task<Optional<ImdbRating>> FindAsync(string tconst, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ImdbRatings.AsNoTracking().SingleOrDefaultAsync(rating => rating.Tconst == tconst, cancellationToken);
		return Optional.Of(row).Map(found => new ImdbRating(found.Average, Some.Of(found.Votes)));
	}

	public async Task<IReadOnlyDictionary<string, ImdbRating>> FindManyAsync(IReadOnlyCollection<string> tconsts, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var found = new Dictionary<string, ImdbRating>();
		foreach (var slice in tconsts.Distinct().Chunk(SliceSize))
		{
			var rows = await ctx.ImdbRatings.AsNoTracking().Where(rating => slice.Contains(rating.Tconst)).ToListAsync(cancellationToken);
			rows.ForEach(row => found[row.Tconst] = new ImdbRating(row.Average, Some.Of(row.Votes)));
		}

		return found;
	}

	public async Task<int> ReplaceAsync(IAsyncEnumerable<ImdbRatingLine> rows, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
		await ctx.ImdbRatings.ExecuteDeleteAsync(cancellationToken);
		await using var command = ctx.Database.GetDbConnection().CreateCommand();
		command.Transaction = transaction.GetDbTransaction();
		command.CommandText = Insert;
		var tconst = Parameter(command, "$tconst", DbType.String);
		var average = Parameter(command, "$average", DbType.Single);
		var votes = Parameter(command, "$votes", DbType.Int32);
		await command.PrepareAsync(cancellationToken);
		var count = 0;
		await foreach (var row in rows.WithCancellation(cancellationToken))
		{
			tconst.Value = row.Tconst;
			average.Value = row.Average;
			votes.Value = row.Votes;
			await command.ExecuteNonQueryAsync(cancellationToken);
			count++;
		}

		if (count == 0)
		{
			throw new InvalidDataException("The IMDb ratings file held no ratings, so the last ones stay.");
		}

		await transaction.CommitAsync(cancellationToken);
		return count;
	}

	private static DbParameter Parameter(DbCommand command, string name, DbType type)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = name;
		parameter.DbType = type;
		command.Parameters.Add(parameter);
		return parameter;
	}
}
