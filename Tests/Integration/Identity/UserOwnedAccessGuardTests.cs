using DB.Data;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Identity;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UserOwnedAccessGuardTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Test]
	public async Task Querying_owned_data_without_an_authenticated_user_throws()
	{
		_ = fixture.Context;
		fixture.CurrentUser.IsAuthenticated = false;

		var threw = false;
		try
		{
			await fixture.Context.Watchlist.AnyAsync();
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}

		await Assert.That(threw).IsTrue();
	}

	[Test]
	public async Task Every_user_owned_entity_has_the_user_query_filter()
	{
		var userOwned = fixture.Context.Model.GetEntityTypes().Where(entityType => typeof(IUserOwned).IsAssignableFrom(entityType.ClrType)).ToList();

		await Assert.That(userOwned).IsNotEmpty();
		foreach (var entityType in userOwned)
		{
			await Assert.That(entityType.GetQueryFilter()).IsNotNull();
		}
	}
}
