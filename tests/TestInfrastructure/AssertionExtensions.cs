using TUnit.Assertions.Exceptions;
using TUnit.Assertions.Sources;

namespace TestInfrastructure;

public static class AssertionExtensions
{
	extension<TValue>(ValueAssertion<TValue> assertion)
	{
		public async Task Multiple(Func<TValue, Task> multiple) => await assertion.Multiple(x => x, multiple);

		private async Task Multiple<TMapped>(Func<TValue, TMapped> selector, Func<TMapped, Task> multiple)
		{
			var valueTuple = await assertion.Context.GetAsync();
			var mapped = selector(valueTuple.Value ?? throw valueTuple.Exception ?? throw new AssertionException("Assertion failed without value or exception."));
			using var _ = Assert.Multiple();
			await multiple(mapped);
		}
	}
}
