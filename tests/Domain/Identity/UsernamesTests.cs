using Domain.Identity;
using TestInfrastructure;

namespace Domain.Tests.Identity;

public class UsernamesTests
{
	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments("mira smith")]
	public async Task Problem_ForAnEmptyNameOrOneWithSpaces_SaysWhy(string username)
	{
		await Assert.That(Usernames.Problem(username)).IsSome();
	}

	[Test]
	public async Task Problem_ForANameLongerThanSixtyFourCharacters_SaysWhy()
	{
		await Assert.That(Usernames.Problem(new string('m', 65))).IsSome();
	}

	[Test]
	[Arguments("mira")]
	[Arguments(" Mira ")]
	[Arguments("bob.smith")]
	public async Task Problem_ForAPlainName_IsNone(string username)
	{
		await Assert.That(Usernames.Problem(username)).IsNone();
	}
}
