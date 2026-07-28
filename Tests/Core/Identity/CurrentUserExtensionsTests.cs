using Contracts.Identity;
using Domain.Identity;
using TestInfrastructure;

namespace Core.Tests.Identity;

public class CurrentUserExtensionsTests
{
	[Test]
	public async Task EnsureAdmin_WhenAdmin_DoesNotThrow()
	{
		var admin = new TestCurrentUser { Role = UserRole.Admin };

		await Assert.That(() => admin.EnsureAdmin()).ThrowsNothing();
	}

	[Test]
	public async Task EnsureAdmin_WhenMember_Throws()
	{
		var member = new TestCurrentUser { Role = UserRole.Member };

		await Assert.That(() => member.EnsureAdmin()).Throws<UnauthorizedAccessException>();
	}
}
