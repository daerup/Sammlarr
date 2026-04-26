using System.Runtime.CompilerServices;
using Shared;
using TUnit.Assertions.Core;
using TUnit.Assertions.Sources;

namespace Test.Shared;

public class UnionAssertion<TOuter, TInner>(AssertionContext<TOuter> context) : Assertion<TInner>(context.Map(MapToInner()))
	where TOuter : IUnion
{
	private static Func<TOuter?, TInner?> MapToInner() =>
		x => x?.Value is TInner inner ? inner : throw new InvalidCastException($"found {(x?.Value?.GetType()).GetGenericName()}");

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<TInner> metadata) =>
		Task.FromResult(metadata.Exception != null ? AssertionResult.Failed(metadata.Exception.Message) : AssertionResult.Passed);

	protected override string GetExpectation() =>
		"union " + TypeExtensions.GetGenericName<TOuter>() + " to be of type " + TypeExtensions.GetGenericName<TInner>();
}

public static class UnionAssertionExtensions
{
	extension<TOuter>(IAssertionSource<TOuter> source)
		where TOuter : IUnion
	{
		public UnionAssertion<TOuter, TInner> IsOfUnionType<TInner>() => new(source.Context);
	}

	extension<TCollection, TItem>(IAssertionSource<TCollection> source)
		where TCollection : IEnumerable<TItem>
		where TItem : IUnion
	{
		public UnionCollectionAssertion<TCollection, TItem> AreOfUnionType(params IEnumerable<Type> expectedTypes)
		{
			using var _ = Assert.Multiple();
			return new UnionCollectionAssertion<TCollection, TItem>(source.Context, expectedTypes);
		}
	}
}

public class UnionCollectionAssertion<TCollection, TItem>(AssertionContext<TCollection> context, IEnumerable<Type> expectedTypes)
	: CollectionAssertionBase<TCollection, TItem>(context)
	where TCollection : IEnumerable<TItem>
	where TItem : IUnion
{
	protected override async Task<AssertionResult> CheckAsync(EvaluationMetadata<TCollection> metadata)
	{
		await Task.CompletedTask;
		if (metadata.Exception is not null)
		{
			return AssertionResult.Failed(metadata.Exception.Message);
		}
		var value = metadata.Value;
		if (value is null)
		{
			return AssertionResult.Failed("value was null");
		}

		if (value.Count() != expectedTypes.Count())
		{
			return AssertionResult.Failed($"expected collection to have {expectedTypes.Count()} items but found {value.Count()}");
		}

		var zipped = value.Zip(expectedTypes, (item, expectedType) => (item, expectedType));
		foreach (var (index, item) in zipped.Index())
		{
			var (actual, expected) = item;
			if (actual is IUnion union)
			{
				if (union.Value!.GetType() == expected)
				{
					continue;
				}

				return AssertionResult.Failed(
					$"at index {index} found '{union.Value.GetType().GetGenericName()}' instead of expected type '{expected.GetGenericName()}'"
				);
			}
			return AssertionResult.Failed($"actual should be union of type {expected.GetGenericName()}");
		}

		return AssertionResult.Passed;
	}

	protected override string GetExpectation() => "unions " + TypeExtensions.GetGenericName<TItem>() + " to all be of expected type";
}
