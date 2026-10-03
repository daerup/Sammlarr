using System.Security.Claims;
using Contracts.Identity;
using Domain.Identity;
using TestInfrastructure;
using UI.Shared.Identity;

namespace UI.Tests.Identity;

public class CurrentUserAccessorTests
{
	private static readonly UserId Ada = UserId.New();

	private readonly CurrentUserOverride _override = new();

	private readonly CurrentUserAccessor Testee;

	public CurrentUserAccessorTests() => Testee = new CurrentUserAccessor(_override);

	[Test]
	public async Task Read_WithoutAnOverride_TakesTheSignedInPrincipal()
	{
		Testee.SetPrincipal(SignedIn(Ada, "Ada", UserRole.Admin));

		await Assert.That(Testee.IsAuthenticated).IsTrue();
		await Assert.That(Testee.Id).IsEqualTo(Ada);
		await Assert.That(Testee.DisplayName).IsEqualTo("Ada");
		await Assert.That(Testee.Role).IsEqualTo(UserRole.Admin);
	}

	[Test]
	public async Task Read_WithAnOverride_TakesTheOverrideOverThePrincipal()
	{
		Testee.SetPrincipal(SignedIn(Ada, "Ada", UserRole.Admin));
		_override.Value = Some.Of<ICurrentUser>(new TestCurrentUser { DisplayName = "Bob", Role = UserRole.Member });

		await Assert.That(Testee.Id).IsEqualTo(TestCurrentUser.DefaultId);
		await Assert.That(Testee.DisplayName).IsEqualTo("Bob");
		await Assert.That(Testee.Role).IsEqualTo(UserRole.Member);
	}

	[Test]
	public async Task Read_WithNobodySignedIn_IsANotAuthenticatedMemberWithoutAnId()
	{
		await Assert.That(Testee.IsAuthenticated).IsFalse();
		await Assert.That(Testee.Role).IsEqualTo(UserRole.Member);
		await Assert.That(() => Testee.Id).Throws<InvalidOperationException>();
	}

	private static ClaimsPrincipal SignedIn(UserId id, string name, UserRole role)
	{
		Claim[] claims = [new(ClaimTypes.NameIdentifier, id.Value.ToString()), new(ClaimTypes.Name, name), new(ClaimTypes.Role, role.ToString())];
		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
	}
}
