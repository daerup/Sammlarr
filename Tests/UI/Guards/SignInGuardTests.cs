using Bunit;
using UI.Shared.Guards;

namespace UI.Tests.Guards;

public class SignInGuardTests : BunitContext
{
	[Test]
	public async Task An_authenticated_user_is_reported_as_having_access()
	{
		AddAuthorization().SetAuthorized("alice");
		bool? access = null;

		Render<SignInGuard>(parameters => parameters.Add(guard => guard.AccessChanged, value => access = value));

		await Assert.That(access).IsTrue();
	}

	[Test]
	public async Task An_anonymous_user_is_reported_as_not_having_access()
	{
		AddAuthorization().SetNotAuthorized();
		bool? access = null;

		Render<SignInGuard>(parameters => parameters.Add(guard => guard.AccessChanged, value => access = value));

		await Assert.That(access).IsFalse();
	}

	[Test]
	public async Task Access_is_reported_once_while_it_stays_the_same()
	{
		AddAuthorization().SetAuthorized("alice");
		var reports = 0;

		var cut = Render<SignInGuard>(parameters => parameters.Add(guard => guard.AccessChanged, _ => reports++));
		cut.Render();

		await Assert.That(reports).IsEqualTo(1);
	}
}
