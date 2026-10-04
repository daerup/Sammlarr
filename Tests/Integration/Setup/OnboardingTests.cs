using Contracts.Auth;
using Contracts.Settings;
using Contracts.Setup;
using Core.Setup;
using Domain.Identity;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Setup;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class OnboardingTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Password = "a long enough password";

	private IOnboardingService Testee => host.Resolve<IOnboardingService>();

	private string SetupCode => host.Resolve<SetupGate>().Code;

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task ClaimAsync_OnAnEmptyInstall_MakesALocalAdminWhoCanSignIn()
	{
		await host.NoUsers();

		var claimed = await Testee.ClaimAsync(new AdminClaim(SetupCode, "Ada", Password, Password));

		await Assert.That(claimed).IsSuccess().And.Multiple(async admin => await Assert.That(admin.Role).IsEqualTo(UserRole.Admin));
		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).AuthProvider).IsEqualTo(AuthProvider.Local);
		await Assert.That(await host.Resolve<ISignInService>().SignInWithPasswordAsync("ada", Password)).IsSuccess();
	}

	[Test]
	public async Task ClaimAsync_WithAWrongSetupCode_CreatesNobody()
	{
		await host.NoUsers();

		var claimed = await Testee.ClaimAsync(new AdminClaim("AAAA-AAAA", "Ada", Password, Password));

		await Assert.That(claimed).IsError();
		await Assert.That(await host.ScopedDbContext.Users.CountAsync()).IsEqualTo(0);
	}

	[Test]
	public async Task ClaimAsync_OnceSomeoneExists_RefusesTheCode()
	{
		var claimed = await Testee.ClaimAsync(new AdminClaim(SetupCode, "Mallory", Password, Password));

		await Assert.That(claimed).IsError();
		await Assert.That(await Testee.NeedsAdminAsync()).IsFalse();
	}

	[Test]
	public async Task ClaimAsync_WithPasswordsThatDiffer_CreatesNobody()
	{
		await host.NoUsers();

		var claimed = await Testee.ClaimAsync(new AdminClaim(SetupCode, "Ada", Password, Password + "!"));

		await Assert.That(claimed).IsError();
		await Assert.That(await Testee.NeedsAdminAsync()).IsTrue();
	}

	[Test]
	public async Task ResetAsync_ThenCompleteAsync_RunsSetupAgainAndEndsIt()
	{
		await Testee.ResetAsync();
		var reset = await Testee.IsCompleteAsync();

		await Testee.CompleteAsync();

		await Assert.That(reset).IsFalse();
		await Assert.That(await Testee.IsCompleteAsync()).IsTrue();
	}

	[Test]
	public async Task CompleteAsync_ForAMember_IsRefused()
	{
		host.CurrentUser.Role = UserRole.Member;

		await Assert.That(async () => await Testee.CompleteAsync()).Throws<UnauthorizedAccessException>();
	}
}
