using Core.Setup;

namespace Core.Tests.Setup;

public class SetupGateTests
{
	private SetupGate Testee { get; } = new();

	[Test]
	public async Task Code_IsTwoGroupsOfFourUnambiguousCharacters()
	{
		await Assert.That(Testee.Code).Matches("^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$");
	}

	[Test]
	public async Task Accepts_TheCodeTypedInLowerCaseWithoutItsDash()
	{
		await Assert.That(Testee.Accepts(Testee.Code.Replace("-", " ").ToLowerInvariant())).IsTrue();
	}

	[Test]
	public async Task Accepts_ACodeOneCharacterOff_Refuses()
	{
		var last = Testee.Code[^1];
		var typo = Testee.Code[..^1] + (last == '2' ? '3' : '2');

		await Assert.That(Testee.Accepts(typo)).IsFalse();
	}
}
