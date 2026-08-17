using Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shared.Options;
using Wolverine.Tracking;

namespace Integration.Tests.Messaging;

public class MessagingBootstrapTests
{
	[Test]
	public async Task Published_message_is_handled_through_the_durable_sqlite_store()
	{
		var dbPath = Path.Combine(Path.GetTempPath(), $"wolverine-test-{Guid.NewGuid():N}.db");
		var connectionString = $"Data Source={dbPath}";
		try
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(
					new Dictionary<string, string?> { [$"{SammlarOptions.SectionName}:{nameof(SammlarOptions.ConnectionString)}"] = connectionString }
				)
				.Build();

			using var host = await Host.CreateDefaultBuilder().UseSammlarrMessaging(configuration).StartAsync();

			var session = await host.TrackActivity().PublishMessageAndWaitAsync(new Ping("hello"));

			await Assert.That(session.Executed.MessagesOf<Ping>().ToList()).Count().IsEqualTo(1);

			await using var connection = new SqliteConnection(connectionString);
			await connection.OpenAsync();
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'wolverine_%'";
			var wolverineTableCount = Convert.ToInt32(await command.ExecuteScalarAsync());
			await Assert.That(wolverineTableCount).IsGreaterThan(0);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(dbPath))
			{
				File.Delete(dbPath);
			}
		}
	}
}
