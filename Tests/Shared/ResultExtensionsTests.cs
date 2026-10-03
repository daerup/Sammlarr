using TestInfrastructure;

namespace Shared.Tests;

public class ResultExtensionsTests
{
	[Test]
	public async Task Match_WhenSuccess_InvokesSuccessBranch()
	{
		Result<int> result = Success.Of(5);

		var mapped = result.Match(value => value * 2, error => -1);

		await Assert.That(mapped).IsEqualTo(10);
	}

	[Test]
	public async Task Match_WhenError_InvokesErrorBranch()
	{
		Result<int> result = Error.From("boom");

		var mapped = result.Match(value => value.ToString(), error => error.Reason);

		await Assert.That(mapped).IsEqualTo("boom");
	}

	[Test]
	public async Task Map_WhenSuccess_TransformsValue()
	{
		Result<int> result = Success.Of(5);

		var mapped = result.Map(value => value.ToString());

		await Assert.That(mapped).IsSuccess().And.IsEqualTo("5");
	}

	[Test]
	public async Task Map_WhenError_PropagatesError()
	{
		Result<int> result = Error.From("boom");

		var mapped = result.Map(value => value.ToString());

		await Assert.That(mapped).IsError();
	}

	[Test]
	public async Task Map_WhenSuccessAndStepReturnsResult_Flattens()
	{
		Result<int> result = Success.Of(4);

		var bound = result.Map(Halve);

		await Assert.That(bound).IsSuccess().And.IsEqualTo(2);
	}

	[Test]
	public async Task Map_WhenStepErrors_PropagatesError()
	{
		Result<int> result = Success.Of(3);

		var bound = result.Map(Halve);

		await Assert.That(bound).IsError();
	}

	[Test]
	public async Task Map_WhenError_DoesNotInvokeStep()
	{
		Result<int> result = Error.From("boom");
		var invoked = false;

		var bound = result.Map(value =>
		{
			invoked = true;
			return Halve(value);
		});

		await Assert.That(invoked).IsFalse();
		await Assert.That(bound).IsError();
	}

	[Test]
	public async Task MapError_WhenError_RewritesReason()
	{
		Result<int> result = Error.From("boom");

		var mapped = result.MapError(error => Error.From($"wrapped: {error.Reason}"));

		await Assert.That(mapped.Match(_ => "ok", error => error.Reason)).IsEqualTo("wrapped: boom");
	}

	[Test]
	public async Task ToOptional_WhenSuccess_IsSome()
	{
		Result<int> result = Success.Of(7);

		await Assert.That(result.ToOptional()).IsSome().And.IsEqualTo(7);
	}

	[Test]
	public async Task ToOptional_WhenError_IsNone()
	{
		Result<int> result = Error.From("boom");

		await Assert.That(result.ToOptional().IsNone()).IsTrue();
	}

	[Test]
	public async Task GetValueOr_WhenError_ReturnsFallback()
	{
		Result<int> result = Error.From("boom");

		await Assert.That(result.GetValueOr(42)).IsEqualTo(42);
		await Assert.That(result.GetValueOr(error => error.Reason.Length)).IsEqualTo(4);
	}

	[Test]
	public async Task Apply_WhenSuccess_InvokesActionAndReturnsSelf()
	{
		Result<int> result = Success.Of(7);
		var captured = 0;

		var applied = result.Apply(value => captured = value);

		await Assert.That(captured).IsEqualTo(7);
		await Assert.That(applied).IsSuccess().And.IsEqualTo(7);
	}

	[Test]
	public async Task ApplyError_WhenError_InvokesAction()
	{
		Result<int> result = Error.From("boom");
		var captured = "";

		result.ApplyError(error => captured = error.Reason);

		await Assert.That(captured).IsEqualTo("boom");
	}

	[Test]
	public async Task MapAsync_WhenSuccess_AwaitsMap()
	{
		Result<int> result = Success.Of(3);

		var mapped = await result.Map(value => Task.FromResult(value + 1));

		await Assert.That(mapped).IsSuccess().And.IsEqualTo(4);
	}

	[Test]
	public async Task MapAsync_WhenErrorAndStepReturnsResult_PropagatesError()
	{
		Result<int> result = Error.From("boom");

		var bound = await result.Map(value => Task.FromResult<Result<string>>(Success.Of(value.ToString())));

		await Assert.That(bound).IsError();
	}

	[Test]
	public async Task MapAsync_ChainsAcrossTasksWithSingleAwait()
	{
		Result<int> start = Success.Of(2);

		var value = await start.Map(v => Task.FromResult(v + 1)).Map(v => v * 10).Map(v => Task.FromResult(v - 5)).GetValueOr(0);

		await Assert.That(value).IsEqualTo(25);
	}

	[Test]
	public async Task MapAsync_WhenErrorMidChain_ShortCircuitsToError()
	{
		Result<int> start = Success.Of(2);

		var outcome = await start
			.Map(v => Task.FromResult(v + 1))
			.Map(_ => (Result<int>)Error.From("boom"))
			.Map(v => Task.FromResult(v * 10))
			.Match(v => v.ToString(), e => e.Reason);

		await Assert.That(outcome).IsEqualTo("boom");
	}

	[Test]
	public async Task Map_WhenStepReturnsSuccess_FlattensToResult()
	{
		Result<int> result = Success.Of(4);

		Result<string> mapped = result.Map(value => Success.Of(value.ToString()));

		await Assert.That(mapped).IsSuccess().And.IsEqualTo("4");
	}

	[Test]
	public async Task ValueTaskMirror_ChainsWithSingleAwait()
	{
		ValueTask<Result<int>> start = new(Success.Of(2));

		var value = await start.Map(v => v + 1).Map(v => Task.FromResult(v * 10)).GetValueOr(0);

		await Assert.That(value).IsEqualTo(30);
	}

	[Test]
	public async Task GetOrThrow_WhenSuccess_ReturnsValue()
	{
		Result<int> result = Success.Of(5);

		await Assert.That(result.GetOrThrow(_ => new InvalidOperationException())).IsEqualTo(5);
	}

	[Test]
	public async Task GetOrThrow_WhenError_Throws()
	{
		Result<int> result = Error.From("boom");

		await Assert
			.That(() => result.GetOrThrow(error => new InvalidOperationException(error.Reason)))
			.Throws<InvalidOperationException>()
			.WithMessageContaining("boom");
	}

	[Test]
	public async Task MatchAsync_WhenSuccess_AwaitsSuccessBranch()
	{
		Result<int> result = Success.Of(3);

		var mapped = await result.Match(value => Task.FromResult(value + 1), error => Task.FromResult(-1));

		await Assert.That(mapped).IsEqualTo(4);
	}

	[Test]
	public async Task MatchAsync_WhenError_AwaitsErrorBranch()
	{
		Result<int> result = Error.From("boom");

		var mapped = await result.Match(value => Task.FromResult(value.ToString()), error => Task.FromResult(error.Reason));

		await Assert.That(mapped).IsEqualTo("boom");
	}

	[Test]
	public async Task MapAsync_WhenStepReturnsSuccess_FlattensToResult()
	{
		Result<int> result = Success.Of(4);

		Result<string> mapped = await result.Map(value => Task.FromResult(Success.Of(value.ToString())));

		await Assert.That(mapped).IsSuccess().And.IsEqualTo("4");
	}

	[Test]
	public async Task ApplyAsync_WhenSuccess_AwaitsActionAndReturnsSelf()
	{
		Result<int> result = Success.Of(7);
		var captured = 0;

		var applied = await result.Apply(CaptureAsync);

		await Assert.That(captured).IsEqualTo(7);
		await Assert.That(applied).IsSuccess().And.IsEqualTo(7);

		async Task CaptureAsync(int value)
		{
			await Task.Yield();
			captured = value;
		}
	}

	[Test]
	public async Task ApplyErrorAsync_WhenError_AwaitsAction()
	{
		Result<int> result = Error.From("boom");
		var captured = "";

		await result.ApplyError(CaptureAsync);

		await Assert.That(captured).IsEqualTo("boom");

		async Task CaptureAsync(Error error)
		{
			await Task.Yield();
			captured = error.Reason;
		}
	}

	[Test]
	public async Task TaskMirror_AppliesMapErrorAndMatch_WithSingleAwait()
	{
		Task<Result<int>> start = Task.FromResult<Result<int>>(Error.From("boom"));
		var captured = "";

		var outcome = await start
			.ApplyError(error => captured = error.Reason)
			.MapError(error => Error.From($"[{error.Reason}]"))
			.Match(value => value.ToString(), error => error.Reason);

		await Assert.That(captured).IsEqualTo("boom");
		await Assert.That(outcome).IsEqualTo("[boom]");
	}

	[Test]
	public async Task WhenNotNull_WhenNull_IsErrorWithReason()
	{
		var result = Result.WhenNotNull((string?)null, "missing");

		await Assert.That(result.Match(_ => "ok", error => error.Reason)).IsEqualTo("missing");
	}

	[Test]
	public async Task WhenNotNull_WhenNotNull_IsSuccess()
	{
		var result = Result.WhenNotNull((string?)"x", "missing");

		await Assert.That(result).IsSuccess().And.IsEqualTo("x");
	}

	[Test]
	public async Task When_WhenSuccessButPredicateFails_IsErrorWithReason()
	{
		Result<string> result = Success.Of("x");

		var filtered = result.When(value => value.Length > 3, "too short");

		await Assert.That(filtered.Match(_ => "ok", error => error.Reason)).IsEqualTo("too short");
	}

	[Test]
	public async Task When_WhenSuccessAndPredicatePasses_IsSuccess()
	{
		Result<string> result = Success.Of("xyzw");

		var filtered = result.When(value => value.Length > 3, "too short");

		await Assert.That(filtered).IsSuccess().And.IsEqualTo("xyzw");
	}

	[Test]
	public async Task When_WhenError_DoesNotInvokePredicateAndKeepsOriginalError()
	{
		Result<string> result = Error.From("boom");

		var filtered = result.When(_ => throw new InvalidOperationException("predicate ran"), "too short");

		await Assert.That(filtered.Match(_ => "ok", error => error.Reason)).IsEqualTo("boom");
	}

	[Test]
	public async Task WhenNotNull_WhenTaskOfNull_IsErrorWithReason()
	{
		var result = await Result.WhenNotNull(Task.FromResult<string?>(null), "missing");

		await Assert.That(result.Match(_ => "ok", error => error.Reason)).IsEqualTo("missing");
	}

	[Test]
	public async Task WhenNotNull_WhenTaskOfNotNull_IsSuccess()
	{
		var result = await Result.WhenNotNull(Task.FromResult<string?>("x"), "missing");

		await Assert.That(result).IsSuccess().And.IsEqualTo("x");
	}

	[Test]
	public async Task IsSuccess_WhenSuccess_IsTrue()
	{
		Result<int> result = Success.Of(5);

		await Assert.That(result.IsSuccess()).IsTrue();
		await Assert.That(result.IsError()).IsFalse();
	}

	[Test]
	public async Task IsSuccess_WhenError_IsFalse()
	{
		Result<int> result = Error.From("boom");

		await Assert.That(result.IsSuccess()).IsFalse();
		await Assert.That(result.IsError()).IsTrue();
	}

	[Test]
	public async Task IsSuccess_OnValueTaskReceiver_AwaitsAndReflectsCase()
	{
		ValueTask<Result<int>> source = new(Success.Of(5));

		await Assert.That(await source.IsSuccess()).IsTrue();
	}

	[Test]
	public async Task WhereSuccess_YieldsOnlySuccessValues()
	{
		var values = new List<int>();

		await foreach (var value in ToAsync<Result<int>>(Success.Of(1), Error.From("x"), Success.Of(3)).WhereSuccessAwait())
		{
			values.Add(value);
		}

		await Assert.That(string.Join(",", values)).IsEqualTo("1,3");
	}

	[Test]
	public async Task WhereSuccess_WithCompanion_YieldsSuccessValuesAndCompanions()
	{
		var pairs = new List<string>();

		await foreach (
			var (value, companion) in ToAsync<(Result<int>, string)>((Success.Of(1), "a"), (Error.From("x"), "b"), (Success.Of(3), "c")).WhereSuccessAwait()
		)
		{
			pairs.Add($"{value}{companion}");
		}

		await Assert.That(string.Join(",", pairs)).IsEqualTo("1a,3c");
	}

	[Test]
	public async Task Match_WithValues_ReturnsSuccessOrErrorConstant()
	{
		Result<int> success = Success.Of(5);
		Result<int> error = Error.From("boom");

		await Assert.That(success.Match("ok", "fail")).IsEqualTo("ok");
		await Assert.That(error.Match("ok", "fail")).IsEqualTo("fail");
	}

	private static async IAsyncEnumerable<T> ToAsync<T>(params T[] items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}

	private static Result<int> Halve(int value) => value % 2 == 0 ? Success.Of(value / 2) : Error.From("odd");
}
