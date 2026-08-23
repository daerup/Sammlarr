using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class UserOwnedAccessGuardTests(SQLiteFixture fixture)
{
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
}
