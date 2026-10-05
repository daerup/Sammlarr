using Roslyn.Providers;

namespace Roslyn.Tests;

public class OwnedByGeneratorTests
{
	private const string Sample = """
		using Shared;

		namespace Sample;

		public enum Owner
		{
			Alpha,
			Beta,
			Gamma,
		}

		public enum Rating
		{
			[OwnedBy<Owner>(Owner.Alpha)]
			[Votes]
			AlphaScore,

			[OwnedBy<Owner>(Owner.Alpha)]
			AlphaCritics,

			[OwnedBy<Owner>(Owner.Beta)]
			[Votes]
			BetaScore,
		}
		""";

	[Test]
	public async Task Generate_ForRatingsWithOwners_EmitsEachOwnerItsVotesAndTheReverse()
	{
		await Verify(await GeneratorRun.OutputAsync(new OwnedByGenerator(), Sample));
	}
}
