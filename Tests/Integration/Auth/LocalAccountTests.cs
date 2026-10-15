using Contracts.Auth;
using Contracts.Identity;
using Domain.Identity;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Auth;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class LocalAccountTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Password = "correct horse battery";

	private const string NewPassword = "staple and a hinge";

	private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

	private ILocalAccountService Testee => host.Resolve<ILocalAccountService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Before(Test)]
	public async Task Background()
	{
		host.SetCurrentTime(Now);
		await host.SignsInWithSammlarrAccounts();
		host.CurrentUser.Id = await host.SeedLocalUser("guest", Password, UserRole.Member);
	}

	[Test]
	public async Task ChangePasswordAsync_WithTheRightCurrentPassword_SignsInWithTheNewOneAndEndsOlderSessions()
	{
		var stamp = await host.SecurityStampOf(host.CurrentUser.Id);

		var changed = await Testee.ChangePasswordAsync(Password, NewPassword);

		await Assert.That(changed).IsSuccess();
		await Assert.That(await host.Resolve<ISignInService>().SignInWithPasswordAsync("guest", NewPassword)).IsSuccess();
		await Assert.That(await host.SecurityStampOf(host.CurrentUser.Id)).IsNotEqualTo(stamp);
		await Assert.That(host.InvalidatedSessions).Contains(host.CurrentUser.Id);
	}

	[Test]
	public async Task ChangePasswordAsync_WithAWrongCurrentPassword_KeepsTheOldOne()
	{
		var changed = await Testee.ChangePasswordAsync("not the password", NewPassword);

		await Assert.That(changed).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("The current password is not right."));
		await Assert.That(await host.Resolve<ISignInService>().SignInWithPasswordAsync("guest", Password)).IsSuccess();
	}

	[Test]
	public async Task ChangePasswordAsync_ToAShortPassword_SaysHowLongItHasToBe()
	{
		var changed = await Testee.ChangePasswordAsync(Password, "too short");

		await Assert.That(changed).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("A password needs at least 12 characters."));
	}
}
