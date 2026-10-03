using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzers.Monads;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MonadBranchAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor Rule = new(
		"SAM0003",
		"Act on an Optional or a Result through its helpers, not through a bool",
		"'{0}' is read as a bool only to choose a branch; write {1}",
		"Design",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "See docs/architecture section 6, Absence and failure."
	);

	private static readonly string[] PresenceReads = ["IsSome", "IsNone", "IsSuccess", "IsError"];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(start =>
		{
			if (MonadTypes.IsShared(start.Compilation))
			{
				return;
			}

			var monads = MonadTypes.For(start.Compilation);
			if (!monads.Present)
			{
				return;
			}

			start.RegisterOperationAction(operation => Analyze(operation, monads), OperationKind.Conditional);
		});
	}

	private static void Analyze(OperationAnalysisContext context, MonadTypes monads)
	{
		if (context.IsGeneratedCode && !Razor.IsGenerated(context.Operation.Syntax.SyntaxTree))
		{
			return;
		}

		var branch = (IConditionalOperation)context.Operation;
		var isExpression = branch.Syntax is ConditionalExpressionSyntax;
		if (!isExpression && LeavesLoop(branch))
		{
			return;
		}

		var read = Reads(branch.Condition, negated: false, monads).FirstOrDefault();
		if (read is null)
		{
			return;
		}

		var combined =
			MonadTypes.Unwrap(branch.Condition) is IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr };
		var receiver = read.Receiver.Syntax.ToString();
		var chain = Chain(context, branch, isExpression, read, receiver);
		var proposal = combined ? $"{chain}, with the other condition inside the step" : chain;
		context.ReportDiagnostic(Diagnostic.Create(Rule, read.Receiver.Syntax.GetLocation(), receiver, proposal));
	}

	private static string Chain(OperationAnalysisContext context, IConditionalOperation branch, bool isExpression, Read read, string receiver)
	{
		var otherwise = read.OnResult ? "ApplyError(error => …)" : "ApplyNone(() => …)";
		var fallback = read.OnResult ? "error => …" : "() => …";
		if (isExpression)
		{
			return $"{receiver}.Match(value => …, {fallback})";
		}

		if (Razor.IsMarkup(branch, context.ContainingSymbol))
		{
			return Markup(read, receiver);
		}

		if (read.Lifted)
		{
			return $"{receiver}.When(…).Apply(value => …).{otherwise}, or let the mapped step return an Optional instead of a bool";
		}

		if (branch.WhenFalse is null && Last(branch.WhenTrue) is IReturnOperation)
		{
			return $"{receiver}.Map(value => …).GetValueOr(…) or {receiver}.Match(value => …, {fallback})";
		}

		if (branch.WhenFalse is not null)
		{
			return $"{receiver}.Apply(value => …).{otherwise}";
		}

		return read.Positive ? $"{receiver}.Apply(value => …)" : $"{receiver}.{otherwise}";
	}

	private static string Markup(Read read, string receiver)
	{
		return (read.OnResult, read.Positive) switch
		{
			(false, true) => $"@if ({receiver} is Some<…>(var value))",
			(false, false) => $"@if ({receiver} is None)",
			(true, true) => $"@if ({receiver} is Success<…>(var value))",
			(true, false) => $"@if ({receiver} is Error error)",
		};
	}

	private static IEnumerable<Read> Reads(IOperation operation, bool negated, MonadTypes monads)
	{
		return MonadTypes.Unwrap(operation) switch
		{
			IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not => Reads(not.Operand, !negated, monads),
			IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } logical => Reads(
					logical.LeftOperand,
					negated,
					monads
				)
				.Concat(Reads(logical.RightOperand, negated, monads)),
			IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } comparison => Compared(comparison, negated, monads),
			IAwaitOperation awaited => Reads(awaited.Operation, negated, monads),
			IInvocationOperation invocation => Read.Of(invocation, negated, monads),
			_ => [],
		};
	}

	private static IEnumerable<Read> Compared(IBinaryOperation comparison, bool negated, MonadTypes monads)
	{
		var flips = comparison.OperatorKind is BinaryOperatorKind.Equals;
		if (Constant(comparison.RightOperand) is { } right)
		{
			return Reads(comparison.LeftOperand, negated ^ (flips != right), monads);
		}

		if (Constant(comparison.LeftOperand) is { } left)
		{
			return Reads(comparison.RightOperand, negated ^ (flips != left), monads);
		}

		return [];
	}

	private static bool? Constant(IOperation operation)
	{
		return MonadTypes.Unwrap(operation).ConstantValue is { HasValue: true, Value: bool value } ? value : null;
	}

	private static bool LeavesLoop(IConditionalOperation branch)
	{
		if (branch.WhenFalse is not null)
		{
			return false;
		}

		return Last(branch.WhenTrue) switch
		{
			IBranchOperation { BranchKind: BranchKind.Continue } => true,
			IBranchOperation { BranchKind: BranchKind.Break } => Nearest(branch, breakable: true) is ILoopOperation,
			IReturnOperation => Nearest(branch, breakable: false) is ILoopOperation,
			_ => false,
		};
	}

	private static IOperation? Nearest(IOperation operation, bool breakable)
	{
		for (var current = operation.Parent; current is not null and not IAnonymousFunctionOperation and not ILocalFunctionOperation; current = current.Parent)
		{
			if (current is ILoopOperation || (breakable && current is ISwitchOperation))
			{
				return current;
			}
		}

		return null;
	}

	private static IOperation Last(IOperation operation)
	{
		return operation is IBlockOperation { Operations.Length: > 0 } block ? block.Operations[block.Operations.Length - 1] : operation;
	}

	private sealed class Read(IOperation receiver, bool positive, bool onResult, bool lifted)
	{
		internal IOperation Receiver { get; } = receiver;

		internal bool Positive { get; } = positive;

		internal bool OnResult { get; } = onResult;

		internal bool Lifted { get; } = lifted;

		internal static IEnumerable<Read> Of(IInvocationOperation invocation, bool negated, MonadTypes monads)
		{
			var receiver = MonadTypes.Receiver(invocation);
			if (receiver is null || !(monads.IsMonad(receiver.Type) || monads.IsAwaitedMonad(receiver.Type)))
			{
				yield break;
			}

			var onResult = monads.IsResult(receiver.Type) || monads.IsResult(Awaited(receiver.Type));
			var method = invocation.TargetMethod;
			if (MonadTypes.IsHelper(method, PresenceReads))
			{
				yield return new Read(receiver, (method.Name is "IsSome" or "IsSuccess") != negated, onResult, lifted: false);
			}
			else if (LiftedReceiver(invocation, receiver) is { } mapped)
			{
				yield return new Read(mapped, !negated, onResult, lifted: true);
			}
		}

		private static IOperation? LiftedReceiver(IInvocationOperation invocation, IOperation receiver)
		{
			if (
				!MonadTypes.IsHelper(invocation.TargetMethod, "GetValueOr")
				|| !invocation.Arguments.Any(argument => argument.Value.ConstantValue is { HasValue: true, Value: bool })
			)
			{
				return null;
			}

			return MonadTypes.Unwrap(receiver) is IInvocationOperation map && MonadTypes.IsHelper(map.TargetMethod, "Map") ? MonadTypes.Receiver(map) : null;
		}

		private static ITypeSymbol? Awaited(ITypeSymbol? type)
		{
			return type is INamedTypeSymbol { TypeArguments.Length: 1 } named ? named.TypeArguments[0] : null;
		}
	}
}
