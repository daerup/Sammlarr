using System.Data;
using System.Data.Common;
using System.Globalization;
using Contracts.Datasets;
using Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DB.Data.Datasets;

internal sealed class ImdbRatingRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IImdbRatings
{
	private const int SliceSize = 1000;

	private const string Prefix = "tt";

	private const float TenthsPerPoint = 10f;

	private const string Insert = "INSERT INTO ImdbRatings (Id, Score, Votes) VALUES ($id, $score, $votes)";

	public async Task<Optional<ImdbRating>> FindAsync(string tconst, CancellationToken cancellationToken = default)
	{
		var found = await FindManyAsync([tconst], cancellationToken);
		return found.GetValueOrNone(tconst);
	}

	public async Task<IReadOnlyDictionary<string, ImdbRating>> FindManyAsync(IReadOnlyCollection<string> tconsts, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var titles = tconsts.Distinct(StringComparer.Ordinal).Select(tconst => NumberOf(tconst).Map(id => (Tconst: tconst, Id: id))).WhereSome().ToList();
		var byId = new Dictionary<int, ImdbRating>();
		foreach (var slice in titles.Select(title => title.Id).Distinct().Chunk(SliceSize))
		{
			var rows = await ctx.ImdbRatings.AsNoTracking().Where(rating => slice.Contains(rating.Id)).ToListAsync(cancellationToken);
			rows.ForEach(row => byId[row.Id] = Rating(row));
		}

		return titles
			.Select(title => byId.GetValueOrNone(title.Id).Map(rating => (title.Tconst, Rating: rating)))
			.WhereSome()
			.ToDictionary(rated => rated.Tconst, rated => rated.Rating, StringComparer.Ordinal);
	}

	public async Task<int> ReplaceAsync(IAsyncEnumerable<ImdbRatingLine> rows, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
		await ctx.ImdbRatings.ExecuteDeleteAsync(cancellationToken);
		await using var command = ctx.Database.GetDbConnection().CreateCommand();
		command.Transaction = transaction.GetDbTransaction();
		command.CommandText = Insert;
		var id = Parameter(command, "$id", DbType.Int32);
		var score = Parameter(command, "$score", DbType.Byte);
		var votes = Parameter(command, "$votes", DbType.Int32);
		await command.PrepareAsync(cancellationToken);
		var count = 0;
		await foreach (var row in rows.WithCancellation(cancellationToken))
		{
			id.Value = NumberOf(row.Tconst).GetValueOr(() => throw new InvalidDataException($"The IMDb ratings file holds an id it cannot read: '{row.Tconst}'."));
			score.Value = (byte)MathF.Round(row.Average * TenthsPerPoint, MidpointRounding.ToEven);
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

	private static Optional<int> NumberOf(string tconst)
	{
		var digits = tconst.StartsWith(Prefix, StringComparison.Ordinal) ? tconst[Prefix.Length..] : string.Empty;
		return Optional.When(int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var id), () => id);
	}

	private static ImdbRating Rating(ImdbRatingRow row) => new(row.Score / TenthsPerPoint, Some.Of(row.Votes));

	private static DbParameter Parameter(DbCommand command, string name, DbType type)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = name;
		parameter.DbType = type;
		command.Parameters.Add(parameter);
		return parameter;
	}
}
