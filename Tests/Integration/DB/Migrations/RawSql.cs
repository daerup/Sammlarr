using Microsoft.Data.Sqlite;

namespace Integration.Tests.DB.Migrations;

internal static class RawSql
{
	internal static string PlexUser(Guid id, string name)
	{
		return $"""
			INSERT INTO Users (Id, CreatedAt, UpdatedAt, Provider, ExternalUserId, DisplayName, TagName, AvatarUrl, Role, JoinedAt, FailedSignIns)
			VALUES ('{id}', 0, 0, 'Plex', '{id}', '{name}', '{name}', '', 'Admin', 0, 0);
			""";
	}

	internal static string Feed(Guid id, Guid owner, string provider)
	{
		return $"""
			INSERT INTO Feeds (Id, CreatedAt, UpdatedAt, UserId, Identifier, IsEnabled, Name, Provider, Strategy, "Trigger")
			VALUES ('{id}', 0, 0, '{owner}', 'list', 1, '{provider} list', '{provider}', '"append"', '"manual"');
			""";
	}

	internal static async Task ExecuteAsync(SqliteConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync();
	}

	internal static async Task<IReadOnlyList<string>> ReadAsync(SqliteConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		await using var reader = await command.ExecuteReaderAsync();
		var values = new List<string>();
		while (await reader.ReadAsync())
		{
			values.Add(reader.GetString(0));
		}

		return values;
	}
}
