using TUnit.Assertions.Core;

namespace TestInfrastructure;

public sealed class SomeAssertion<T>(AssertionContext<Optional<T>> context) : Assertion<T>(context.Map(Unwrap()))
	where T : notnull
{
	private static Func<Optional<T>, T?> Unwrap() => optional => optional.Match(value => (T?)value, () => throw new InvalidCastException("found None"));

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<T> metadata) =>
		Task.FromResult(metadata.Exception != null ? AssertionResult.Failed(metadata.Exception.Message) : AssertionResult.Passed);

	protected override string GetExpectation() => "Optional<" + TypeExtensions.GetGenericName<T>() + "> to be Some";
}

public static class OptionalAssertionExtensions
{
	extension<T>(IAssertionSource<Optional<T>> source)
		where T : notnull
	{
		public SomeAssertion<T> IsSome() => new(source.Context);

		public UnionAssertion<Optional<T>, None> IsNone() => source.IsOfUnionType<Optional<T>, None>();
	}
}
