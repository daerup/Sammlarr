using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Shared.Tests;

public class OptionalExtensionsTests
{
	[Test]
	public async Task Match_WhenSome_InvokesSomeBranch()
	{
		Optional<int> optional = Some.Of(5);

		var result = optional.Match(value => value * 2, () => -1);

		await Assert.That(result).IsEqualTo(10);
	}

	[Test]
	public async Task Match_WhenNone_InvokesNoneBranch()
	{
		Optional<int> optional = new None();

		var result = optional.Match(value => value * 2, () => -1);

		await Assert.That(result).IsEqualTo(-1);
	}

	[Test]
	public async Task Map_WhenSome_TransformsValue()
	{
		Optional<int> optional = Some.Of(5);

		var result = optional.Map(value => value.ToString());

		await Assert.That(result).IsSome().And.IsEqualTo("5");
	}

	[Test]
	public async Task Map_WhenNone_StaysNone()
	{
		Optional<int> optional = new None();

		var result = optional.Map(value => value.ToString());

		await Assert.That(result).IsNone();
	}

	[Test]
	public async Task Map_WhenSomeAndStepReturnsOptional_Flattens()
	{
		Optional<int> optional = Some.Of(4);

		var result = optional.Map(ToLabel);

		await Assert.That(result).IsSome().And.IsEqualTo("n4");
	}

	[Test]
	public async Task Map_WhenStepReturnsNone_IsNone()
	{
		Optional<int> optional = Some.Of(0);

		var result = optional.Map(ToLabel);

		await Assert.That(result).IsNone();
	}

	[Test]
	public async Task Map_WhenNone_DoesNotInvokeStep()
	{
		Optional<int> optional = new None();
		var invoked = false;

		var result = optional.Map(value =>
		{
			invoked = true;
			return ToLabel(value);
		});

		await Assert.That(invoked).IsFalse();
		await Assert.That(result).IsNone();
	}

	[Test]
	public async Task GetValueOr_WhenNone_ReturnsFallback()
	{
		Optional<int> optional = new None();

		await Assert.That(optional.GetValueOr(42)).IsEqualTo(42);
		await Assert.That(optional.GetValueOr(() => 43)).IsEqualTo(43);
	}

	[Test]
	public async Task Or_WhenNone_ReturnsFallbackOptional()
	{
		Optional<int> optional = new None();

		var result = optional.Or(Some.Of(9));

		await Assert.That(result).IsSome().And.IsEqualTo(9);
	}

	[Test]
	public async Task Or_WhenSome_KeepsOriginal()
	{
		Optional<int> optional = Some.Of(1);

		var result = optional.Or(() => Some.Of(9));

		await Assert.That(result).IsSome().And.IsEqualTo(1);
	}

	[Test]
	public async Task Or_WhenNoneAndAsyncFallback_ReturnsFallback()
	{
		Optional<int> optional = new None();

		var result = await optional.Or(() => Task.FromResult<Optional<int>>(Some.Of(9)));

		await Assert.That(result).IsSome().And.IsEqualTo(9);
	}

	[Test]
	public async Task Or_WhenSomeAndAsyncFallback_KeepsOriginalWithoutInvoking()
	{
		Optional<int> optional = Some.Of(1);
		var invoked = false;

		var result = await optional.Or(() =>
		{
			invoked = true;
			return Task.FromResult<Optional<int>>(Some.Of(9));
		});

		await Assert.That(result).IsSome().And.IsEqualTo(1);
		await Assert.That(invoked).IsFalse();
	}

	[Test]
	public async Task Apply_WhenSome_InvokesActionAndReturnsSelf()
	{
		Optional<int> optional = Some.Of(7);
		var captured = 0;

		var result = optional.Apply(value => captured = value);

		await Assert.That(captured).IsEqualTo(7);
		await Assert.That(result).IsSome().And.IsEqualTo(7);
	}

	[Test]
	public async Task Apply_WhenNone_DoesNotInvokeAction()
	{
		Optional<int> optional = new None();
		var captured = 0;

		optional.Apply(value => captured = value);

		await Assert.That(captured).IsEqualTo(0);
	}

	[Test]
	public async Task MatchAsync_WhenSome_AwaitsSomeBranch()
	{
		Optional<int> optional = Some.Of(3);

		var result = await optional.Match(value => Task.FromResult(value + 1), () => Task.FromResult(-1));

		await Assert.That(result).IsEqualTo(4);
	}

	[Test]
	public async Task MapAsync_WhenSome_AwaitsMap()
	{
		Optional<int> optional = Some.Of(3);

		var result = await optional.Map(value => Task.FromResult(value + 1));

		await Assert.That(result).IsSome().And.IsEqualTo(4);
	}

	[Test]
	public async Task MapAsync_WhenNoneAndStepReturnsOptional_IsNone()
	{
		Optional<int> optional = new None();

		var result = await optional.Map(value => Task.FromResult<Optional<string>>(Some.Of(value.ToString())));

		await Assert.That(result).IsNone();
	}

	[Test]
	public async Task MapAsync_ChainsAcrossTasksWithSingleAwait()
	{
		Optional<int> start = Some.Of(2);

		var value = await start.Map(v => Task.FromResult(v + 1)).Map(v => v * 10).Map(v => Task.FromResult(v - 5)).GetValueOr(0);

		await Assert.That(value).IsEqualTo(25);
	}

	[Test]
	public async Task Map_WhenStepReturnsSome_FlattensToOptional()
	{
		Optional<int> optional = Some.Of(4);

		Optional<string> mapped = optional.Map(value => Some.Of(value.ToString()));

		await Assert.That(mapped).IsSome().And.IsEqualTo("4");
	}

	[Test]
	public async Task ApplyAsync_WhenSome_AwaitsActionAndReturnsSelf()
	{
		Optional<int> optional = Some.Of(7);
		var captured = 0;

		var applied = await optional.Apply(CaptureAsync);

		await Assert.That(captured).IsEqualTo(7);
		await Assert.That(applied).IsSome().And.IsEqualTo(7);

		async Task CaptureAsync(int value)
		{
			await Task.Yield();
			captured = value;
		}
	}

	[Test]
	public async Task ValueTaskMirror_ChainsWithSingleAwait()
	{
		ValueTask<Optional<int>> start = new(Some.Of(2));

		var value = await start.Map(v => v + 1).Map(v => Task.FromResult(v * 10)).GetValueOr(0);

		await Assert.That(value).IsEqualTo(30);
	}

	[Test]
	public async Task Of_WhenNull_IsNone()
	{
		await Assert.That(Optional.Of((string?)null)).IsNone();
	}

	[Test]
	public async Task Of_WhenNotNull_IsSome()
	{
		await Assert.That(Optional.Of((string?)"x")).IsSome().And.IsEqualTo("x");
	}

	[Test]
	public async Task Of_WhenTaskOfNull_IsNone()
	{
		await Assert.That(await Optional.Of(Task.FromResult<string?>(null))).IsNone();
	}

	[Test]
	public async Task When_WhenConditionFalse_IsNone()
	{
		Optional<int> optional = Some.Of(5);

		await Assert.That(optional.When(false)).IsNone();
	}

	[Test]
	public async Task When_WhenPredicateFalse_IsNone()
	{
		Optional<int> optional = Some.Of(5);

		await Assert.That(optional.When(value => value > 10)).IsNone();
	}

	[Test]
	public async Task When_WhenPredicateTrue_KeepsValue()
	{
		Optional<int> optional = Some.Of(5);

		await Assert.That(optional.When(value => value > 1)).IsSome().And.IsEqualTo(5);
	}

	[Test]
	public async Task WhenAsync_WhenPredicateFalse_IsNone()
	{
		Optional<int> optional = Some.Of(5);

		var result = await optional.When(value => Task.FromResult(value > 10));

		await Assert.That(result).IsNone();
	}

	[Test]
	public async Task ToResult_WhenSome_IsSuccessWithValue()
	{
		Optional<int> optional = Some.Of(5);

		await Assert.That(optional.ToResult("missing")).IsSuccess().And.IsEqualTo(5);
	}

	[Test]
	public async Task ToResult_WhenNone_IsErrorWithReason()
	{
		Optional<int> optional = new None();

		await Assert.That(optional.ToResult("missing").Match(_ => "ok", error => error.Reason)).IsEqualTo("missing");
	}

	[Test]
	public async Task IsSome_WhenSome_IsTrue()
	{
		Optional<int> optional = Some.Of(5);

		await Assert.That(optional.IsSome()).IsTrue();
		await Assert.That(optional.IsNone()).IsFalse();
	}

	[Test]
	public async Task IsSome_WhenNone_IsFalse()
	{
		Optional<int> optional = new None();

		await Assert.That(optional.IsSome()).IsFalse();
		await Assert.That(optional.IsNone()).IsTrue();
	}

	[Test]
	public async Task IsSome_OnTaskReceiver_AwaitsAndReflectsCase()
	{
		await Assert.That(await Task.FromResult<Optional<int>>(Some.Of(5)).IsSome()).IsTrue();
	}

	[Test]
	public async Task FirstSomeAsync_ReturnsFirstSome()
	{
		var result = await new[] { 0, 0, 2 }.FirstSomeAsync(n => Task.FromResult(ToLabel(n)));

		await Assert.That(result).IsSome().And.IsEqualTo("n2");
	}

	[Test]
	public async Task FirstSomeAsync_WhenAllNone_IsNone()
	{
		var result = await new[] { 0, -1, 0 }.FirstSomeAsync(n => Task.FromResult(ToLabel(n)));

		await Assert.That(result).IsNone();
	}

	[Test]
	public async Task FirstSomeAsync_ShortCircuitsAfterFirstSome()
	{
		var probed = new List<int>();

		await new[] { 0, 2, 3 }.FirstSomeAsync(n =>
		{
			probed.Add(n);
			return Task.FromResult(ToLabel(n));
		});

		await Assert.That(probed.Count).IsEqualTo(2);
	}

	[Test]
	public async Task Match_WithValues_ReturnsSomeOrNoneConstant()
	{
		Optional<int> some = Some.Of(5);
		Optional<int> none = new None();

		await Assert.That(some.Match("some", "none")).IsEqualTo("some");
		await Assert.That(none.Match("some", "none")).IsEqualTo("none");
	}

	[Test]
	public async Task ApplyNone_WhenNone_InvokesActionAndReturnsSelf()
	{
		Optional<int> optional = new None();
		var invoked = false;

		var result = optional.ApplyNone(() => invoked = true);

		await Assert.That(invoked).IsTrue();
		await Assert.That(result).IsNone();
	}

	[Test]
	public async Task ApplyNone_WhenSome_DoesNotInvoke()
	{
		Optional<int> optional = Some.Of(5);
		var invoked = false;

		optional.ApplyNone(() => invoked = true);

		await Assert.That(invoked).IsFalse();
	}

	[Test]
	public async Task ApplyNone_WhenNoneAndAsyncAction_AwaitsAction()
	{
		Optional<int> optional = new None();
		var invoked = false;

		var result = await optional.ApplyNone(InvokeAsync);

		await Assert.That(invoked).IsTrue();
		await Assert.That(result).IsNone();

		async Task InvokeAsync()
		{
			await Task.Yield();
			invoked = true;
		}
	}

	[Test]
	public async Task FirstOrNoneAsync_ReturnsFirstMatchSkippingNulls()
	{
		var result = await ToAsync(null, "a", "bb", "ccc").FirstOrNoneAsync(s => s.Length >= 2);

		await Assert.That(result).IsSome().And.IsEqualTo("bb");
	}

	[Test]
	public async Task FirstOrNoneAsync_WhenNoMatch_IsNone()
	{
		var result = await ToAsync(null, "a").FirstOrNoneAsync(s => s.Length >= 5);

		await Assert.That(result).IsNone();
	}

	[Test]
	public async Task Of_WhenNullValueType_IsNone()
	{
		await Assert.That(Optional.Of((int?)null)).IsNone();
	}

	[Test]
	public async Task Of_WhenNonNullValueType_IsSome()
	{
		await Assert.That(Optional.Of((int?)4)).IsSome().And.IsEqualTo(4);
	}

	[Test]
	public async Task When_WhenTheConditionHolds_IsTheBuiltValue()
	{
		await Assert.That(Optional.When(true, () => 4)).IsSome().And.IsEqualTo(4);
	}

	[Test]
	public async Task When_WhenTheConditionFails_IsNoneWithoutBuildingTheValue()
	{
		await Assert.That(Optional.When<int>(false, () => throw new InvalidOperationException("built"))).IsNone();
	}

	[Test]
	public async Task ToNullable_WhenSomeReference_IsTheValue()
	{
		Optional<string> optional = Some.Of("x");

		await Assert.That(optional.ToNullable()).IsEqualTo("x");
	}

	[Test]
	public async Task ToNullable_WhenNoneReference_IsNull()
	{
		Optional<string> optional = new None();

		await Assert.That(optional.ToNullable()).IsNull();
	}

	[Test]
	public async Task ToNullable_WhenSomeValueType_IsTheValue()
	{
		Optional<int> optional = Some.Of(3);

		await Assert.That(optional.ToNullable()).IsEqualTo(3);
	}

	[Test]
	public async Task ToNullable_WhenNoneValueType_IsNull()
	{
		Optional<int> optional = new None();

		await Assert.That(optional.ToNullable()).IsNull();
	}

	[Test]
	public async Task GetValueOrNone_WhenTheKeyIsPresent_IsSome()
	{
		var source = new Dictionary<string, int> { ["a"] = 1 };

		await Assert.That(source.GetValueOrNone("a")).IsSome().And.IsEqualTo(1);
	}

	[Test]
	public async Task GetValueOrNone_WhenTheKeyIsMissing_IsNone()
	{
		var source = new Dictionary<string, int> { ["a"] = 1 };

		await Assert.That(source.GetValueOrNone("b")).IsNone();
	}

	[Test]
	public async Task WhereSome_WhenSomeAndNoneMixed_KeepsThePresentValuesInOrder()
	{
		Optional<int>[] source = [Some.Of(3), new None(), Some.Of(1)];

		await Assert.That(source.WhereSome()).IsEquivalentTo([3, 1], CollectionOrdering.Matching);
	}

	[Test]
	public async Task FirstOrNone_WhenAnItemMatches_IsTheFirstMatch()
	{
		await Assert.That(new[] { "a", "bb", "ccc" }.FirstOrNone(s => s.Length >= 2)).IsSome().And.IsEqualTo("bb");
	}

	[Test]
	public async Task FirstOrNone_WhenNoItemMatches_IsNone()
	{
		await Assert.That(new[] { "a" }.FirstOrNone(s => s.Length >= 2)).IsNone();
	}

	[Test]
	public async Task When_WhenConditionTrue_KeepsValue()
	{
		Optional<int> optional = Some.Of(5);

		await Assert.That(optional.When(true)).IsSome().And.IsEqualTo(5);
	}

	[Test]
	public async Task When_WhenNone_IsNoneWithoutAskingThePredicate()
	{
		Optional<int> optional = new None();
		var asked = false;

		var result = optional.When(_ => asked = true);

		await Assert.That(result).IsNone();
		await Assert.That(asked).IsFalse();
	}

	[Test]
	public async Task Or_WhenSomeAndOptionalFallback_KeepsOriginal()
	{
		Optional<int> optional = Some.Of(1);

		await Assert.That(optional.Or(Some.Of(9))).IsSome().And.IsEqualTo(1);
	}

	[Test]
	public async Task ToResult_WhenNoneAndAnError_IsThatError()
	{
		Optional<int> optional = new None();
		var error = Error.From("missing");

		await Assert.That(optional.ToResult(error)).IsError().And.IsSameReferenceAs(error);
	}

	[Test]
	public async Task SomeOf_WhenGivenATask_AwaitsItsValue()
	{
		var some = await Some.Of(Task.FromResult(6));

		await Assert.That(some.Value).IsEqualTo(6);
	}

	[Test]
	public async Task Apply_WhenChainedOnATask_FinishesTheAsyncStepBeforeTheNext()
	{
		var steps = new List<string>();
		var start = Task.FromResult<Optional<int>>(Some.Of(1));

		var present = await start.Apply(_ => FirstAsync()).Apply(_ => steps.Add("second")).IsSome();

		await Assert.That(present).IsTrue();
		await Assert.That(steps).IsEquivalentTo(["first", "second"], CollectionOrdering.Matching);

		async Task FirstAsync()
		{
			await Task.Delay(20);
			steps.Add("first");
		}
	}

	private static async IAsyncEnumerable<T> ToAsync<T>(params T[] items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}

	private static Optional<string> ToLabel(int value) => Optional.When(value > 0, () => $"n{value}");
}
