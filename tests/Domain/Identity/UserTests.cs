using Domain.Identity;

namespace Domain.Tests.Identity;

public class UserTests
{
	private static readonly User Testee = new(UserId.New(), "Ada", string.Empty, UserRole.Member, DateTimeOffset.UnixEpoch);

	[Test]
	public async Task TagName_ForANewPerson_IsTheirDisplayName()
	{
		await Assert.That(Testee.TagName).IsEqualTo("Ada");
	}

	[Test]
	public async Task TagName_AfterARename_StaysTheNameTheyJoinedWith()
	{
		await Assert.That((Testee with { DisplayName = "Ada Lovelace" }).TagName).IsEqualTo("Ada");
	}
}
