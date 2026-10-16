using Roslyn.Providers;

namespace Roslyn.Tests;

public class SuppliedByGeneratorTests
{
	private const string Sample = """
		using Shared;

		namespace Sample;

		public enum Supplier
		{
			Alpha,
			Beta,
			Gamma,
		}

		public enum Source
		{
			[SuppliedBy<Supplier>(Supplier.Alpha, Supplier.Beta)]
			First,

			[SuppliedBy<Supplier>(Supplier.Beta)]
			Second,

			Third,
		}
		""";

	[Test]
	public async Task Generate_ForSourcesWithSuppliers_EmitsEachSourcesSuppliersAndTheReverse()
	{
		await Verify(await GeneratorRun.OutputAsync(new SuppliedByGenerator(), Sample));
	}
}
