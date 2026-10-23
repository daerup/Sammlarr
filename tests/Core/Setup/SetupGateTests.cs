using Core.Setup;
using Shared.Options;

namespace Core.Tests.Setup;

public class SetupGateTests
{
	private SetupGate Testee { get; } = new(new AccountsOptions());

	[Test]
	public async Task Code_IsTwoGroupsOfFourUnambiguousCharacters()
	{
		await Assert.That(Testee.Code.Reveal()).Matches("^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$");
	}

	[Test]
	public async Task Accepts_TheCodeTypedInLowerCaseWithoutItsDash()
	{
		await Assert.That(Testee.Accepts(new Secret(Testee.Code.Reveal().Replace("-", " ").ToLowerInvariant()))).IsTrue();
	}

	[Test]
	public async Task Accepts_ACodeOneCharacterOff_Refuses()
	{
		var last = Testee.Code.Reveal()[^1];
		var typo = Testee.Code.Reveal()[..^1] + (last == '2' ? '3' : '2');

		await Assert.That(Testee.Accepts(new Secret(typo))).IsFalse();
	}

	[Test]
	public async Task Code_WhenOneIsConfigured_IsThatCodeAndAcceptedAsTyped()
	{
		var gate = new SetupGate(new AccountsOptions { SetupCode = new Secret("samm larr") });

		await Assert.That(gate.Code.Reveal()).IsEqualTo("SAMM-LARR");
		await Assert.That(gate.Accepts(new Secret("Samm-Larr"))).IsTrue();
	}

	[Test]
	[Arguments("SAMM-LAR")]
	[Arguments("SAMM-LARR-2")]
	[Arguments("SAMM-L0RR")]
	public async Task Code_WhenTheConfiguredOneIsNotEightOfItsCharacters_Refuses(string configured)
	{
		await Assert.That(() => new SetupGate(new AccountsOptions { SetupCode = new Secret(configured) })).Throws<InvalidOperationException>();
	}
}
