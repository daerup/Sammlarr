using DB.Data;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Identity;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UserOwnedAccessGuardTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Test]
	public async Task QueryFilter_WhenNoUserIsAuthenticated_Throws()
	{
		_ = fixture.Context;
		fixture.CurrentUser.IsAuthenticated = false;

		var threw = false;
		try
		{
			await fixture.Context.Wantlist.AnyAsync();
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}

		await Assert.That(threw).IsTrue();
	}

	[Test]
	public async Task QueryFilter_ForEveryUserOwnedEntity_IsApplied()
	{
		var userOwned = fixture.Context.Model.GetEntityTypes().Where(entityType => typeof(IUserOwned).IsAssignableFrom(entityType.ClrType)).ToList();

		await Assert.That(userOwned).IsNotEmpty();
		foreach (var entityType in userOwned)
		{
			await Assert.That(entityType.GetDeclaredQueryFilters()).IsNotEmpty();
		}
	}
}
