using DB.Data.Notifications;
using Domain.Notifications;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Notifications;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class StoredEventTypeFormatTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Test]
	public async Task EventTypes_AreStoredAsAJsonArrayOfQuotedNames()
	{
		var targets = new NotificationTargetRepository(fixture.ContextFactory);
		var subscriptions = new NotificationSubscriptionRepository(fixture.ContextFactory);
		var targetId = await targets.CreateAsync(new NotificationTarget(Guid.Empty, "Target", true, new PushoverConfig(new Secret("token"), new Secret("key"))));

		await subscriptions.AddAsync(targetId, [NotificationEventType.WantlistSyncCompleted, NotificationEventType.WantlistSyncFailed], NotificationTemplate.None);

		await using var context = await fixture.ContextFactory.CreateDbContextAsync();
		var connection = context.Database.GetDbConnection();
		await connection.OpenAsync();
		await using var counter = connection.CreateCommand();
		counter.CommandText = """SELECT count(*) FROM "NotificationSubscriptions" """;
		await Assert.That(Convert.ToInt32(await counter.ExecuteScalarAsync())).IsEqualTo(1);

		await using var command = connection.CreateCommand();
		command.CommandText = """SELECT "EventTypes" FROM "NotificationSubscriptions" """;

		await Assert.That((string?)await command.ExecuteScalarAsync()).IsEqualTo("""["WantlistSyncCompleted","WantlistSyncFailed"]""");
	}
}
