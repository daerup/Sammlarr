using Core.Setup;
using Shared.Options;

namespace Core.Tests.Setup;

public class SetupGateTests
{
	private SetupGate Testee { get; } = new(new AccountsOptions());

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

	[Test]
	public async Task Code_WhenOneIsConfigured_IsThatCodeAndAcceptedAsTyped()
	{
		var gate = new SetupGate(new AccountsOptions { SetupCode = "samm larr" });

		await Assert.That(gate.Code).IsEqualTo("SAMM-LARR");
		await Assert.That(gate.Accepts("Samm-Larr")).IsTrue();
	}

	[Test]
	[Arguments("SAMM-LAR")]
	[Arguments("SAMM-LARR-2")]
	[Arguments("SAMM-L0RR")]
	public async Task Code_WhenTheConfiguredOneIsNotEightOfItsCharacters_Refuses(string configured)
	{
		await Assert.That(() => new SetupGate(new AccountsOptions { SetupCode = configured })).Throws<InvalidOperationException>();
	}
}
