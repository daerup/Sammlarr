using System.Runtime.CompilerServices;
using Shared;
using TUnit.Assertions.Core;

namespace Test.Shared;

public class UnionAssertion<TOuter, TInner>(AssertionContext<TOuter> context) : Assertion<TInner>(context.Map(MapToInner()))
	where TOuter : IUnion
{
	private static Func<TOuter?, TInner?> MapToInner()
	{
		return x => x?.Value is TInner inner ? inner : throw new InvalidCastException($"found {(x?.Value?.GetType()).GetGenericName()}");
	}

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<TInner> metadata)
	{
		return Task.FromResult(metadata.Exception != null ? AssertionResult.Failed(metadata.Exception.Message) : AssertionResult.Passed);
	}

	protected override string GetExpectation() =>
		"union " + TypeExtensions.GetGenericName<TOuter>() + " to be of type " + TypeExtensions.GetGenericName<TInner>();
}

public static class UnionAssertionExtensions
{
	extension<TOuter>(IAssertionSource<TOuter> source)
		where TOuter : IUnion
	{
		public UnionAssertion<TOuter, TInner> IsOfUnionType<TInner>()
		{
			return new UnionAssertion<TOuter, TInner>(source.Context);
		}
	}
}
