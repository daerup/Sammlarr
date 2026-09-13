using Bunit;
using UI.Shared.Guards;

namespace UI.Tests.Guards;

public class SignInGuardTests : BunitContext
{
	[Test]
	public async Task Access_WhenTheUserIsAuthenticated_IsReportedAsGranted()
	{
		AddAuthorization().SetAuthorized("alice");
		bool? access = null;

		Render<SignInGuard>(parameters => parameters.Add(guard => guard.AccessChanged, value => access = value));

		await Assert.That(access).IsTrue();
	}

	[Test]
	public async Task Access_WhenTheUserIsAnonymous_IsReportedAsDenied()
	{
		AddAuthorization().SetNotAuthorized();
		bool? access = null;

		Render<SignInGuard>(parameters => parameters.Add(guard => guard.AccessChanged, value => access = value));

		await Assert.That(access).IsFalse();
	}

	[Test]
	public async Task Access_WhenUnchanged_IsReportedOnce()
	{
		AddAuthorization().SetAuthorized("alice");
		var reports = 0;

		var cut = Render<SignInGuard>(parameters => parameters.Add(guard => guard.AccessChanged, _ => reports++));
		cut.Render();

		await Assert.That(reports).IsEqualTo(1);
	}
}
