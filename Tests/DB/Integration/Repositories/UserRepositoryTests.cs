using Contracts.Identity;
using DB.Data.Identity;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class UserRepositoryTests(SQLiteFixture fixture)
{
	private IUserRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup() => _testee = new UserRepository(fixture.ContextFactory);

	[Test]
	public async Task GetAsync_ReturnsStoredUserWithRealRole()
	{
		var user = await _testee.GetAsync(fixture.CurrentUser.Id);

		await Assert.That(user.Id).IsEqualTo(fixture.CurrentUser.Id);
		await Assert.That(user.Role).IsEqualTo(fixture.CurrentUser.Role);
		await Assert.That(user.DisplayName).IsEqualTo(fixture.CurrentUser.DisplayName);
	}
}
