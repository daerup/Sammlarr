using Contracts.Auth;
using Contracts.Identity;
using Domain.Identity;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Auth;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class LocalAccountTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly Secret Password = new("correct horse battery");

	private static readonly Secret NewPassword = new("staple and a hinge");

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
	public async Task ChangePasswordAsync_AfterFiveWrongCurrentPasswords_LocksTheAccount()
	{
		for (var attempt = 0; attempt < 5; attempt++)
		{
			await Testee.ChangePasswordAsync(new Secret("not the password"), NewPassword);
		}

		var changed = await Testee.ChangePasswordAsync(Password, NewPassword);

		await Assert
			.That(changed)
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("Too many failed sign-ins. Try again in a few minutes."));
		await Assert
			.That(await host.Resolve<ISignInService>().SignInWithPasswordAsync("guest", Password))
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("Too many failed sign-ins. Try again in a few minutes."));
	}

	[Test]
	public async Task ChangePasswordAsync_WithAWrongCurrentPassword_KeepsTheOldOne()
	{
		var changed = await Testee.ChangePasswordAsync(new Secret("not the password"), NewPassword);

		await Assert.That(changed).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("The current password is not right."));
		await Assert.That(await host.Resolve<ISignInService>().SignInWithPasswordAsync("guest", Password)).IsSuccess();
	}

	[Test]
	public async Task ChangePasswordAsync_ToAShortPassword_SaysHowLongItHasToBe()
	{
		var changed = await Testee.ChangePasswordAsync(Password, new Secret("too short"));

		await Assert.That(changed).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("A password needs at least 12 characters."));
	}
}
