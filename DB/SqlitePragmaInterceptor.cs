using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DB;

internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
	private const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA journal_size_limit=8388608;";

	public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = Pragmas;
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
	{
		using var command = connection.CreateCommand();
		command.CommandText = Pragmas;
		command.ExecuteNonQuery();
	}
}
