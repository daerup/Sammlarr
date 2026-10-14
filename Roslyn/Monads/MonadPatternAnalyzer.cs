using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Roslyn.Monads;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MonadPatternAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor Rule = new(
		"SAM0002",
		"Read an Optional or a Result through its helpers",
		"Use {0} instead of {1}",
		"Design",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "See docs/architecture section 6, Absence and failure."
	);

	private static readonly string[] Steps = ["Apply", "ApplyNone", "ApplyError"];

	private static readonly string[] OrderingKeys = ["OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "MinBy", "MaxBy"];

	private static readonly OperationKind[] Kinds =
	[
		OperationKind.IsPattern,
		OperationKind.SwitchExpression,
		OperationKind.Switch,
		OperationKind.Conditional,
		OperationKind.Invocation,
		OperationKind.Interpolation,
		OperationKind.Binary,
		OperationKind.Conversion,
		OperationKind.DefaultValue,
		OperationKind.ObjectCreation,
	];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

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

			start.RegisterOperationAction(operation => Analyze(operation, monads), Kinds);
		});
	}

	private static void Analyze(OperationAnalysisContext context, MonadTypes monads)
	{
		if (context.IsGeneratedCode && !Razor.IsGenerated(context.Operation.Syntax.SyntaxTree))
		{
			return;
		}

		switch (context.Operation)
		{
			case IIsPatternOperation isPattern:
				AnalyzePattern(context, isPattern, monads);
				break;
			case ISwitchExpressionOperation switchExpression:
				AnalyzeSwitch(context, switchExpression.Value, switchExpression.Arms.Select(arm => arm.Pattern), monads);
				break;
			case ISwitchOperation switchStatement:
				AnalyzeSwitch(context, switchStatement.Value, CasePatterns(switchStatement), monads);
				break;
			case IConditionalOperation conditional:
				AnalyzeConstruction(context, conditional, monads);
				break;
			case IInvocationOperation invocation:
				AnalyzeInvocation(context, invocation, monads);
				break;
			case IInterpolationOperation interpolation:
				AnalyzeText(context, interpolation.Expression, monads);
				break;
			case IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } concatenation:
				AnalyzeText(context, concatenation.LeftOperand, monads);
				AnalyzeText(context, concatenation.RightOperand, monads);
				break;
			case IConversionOperation conversion:
				AnalyzeNullConversion(context, conversion, monads);
				break;
			case IDefaultValueOperation defaultValue when monads.IsMonad(defaultValue.Type):
				Report(context, defaultValue, Missing(monads, defaultValue.Type), "a default value, which holds no case");
				break;
			case IObjectCreationOperation { Arguments.Length: 0, Initializer: null } creation when monads.IsMonad(creation.Type):
				Report(context, creation, Missing(monads, creation.Type), "an empty constructor call, which builds a value holding no case");
				break;
		}
	}

	private static void AnalyzePattern(OperationAnalysisContext context, IIsPatternOperation isPattern, MonadTypes monads)
	{
		var patterns = MonadPatterns(isPattern.Pattern, monads);
		if (patterns.Count == 0)
		{
			return;
		}

		var optional = patterns.Any(pattern => monads.IsOptional(pattern.InputType));
		if (patterns.Any(pattern => IsShapeTest(pattern, monads)))
		{
			Report(context, isPattern, optional ? "IsNone() or IsSome()" : "IsError() or IsSuccess()", "a null, constant or { } pattern, which never matches a case");
			return;
		}

		var condition = ConditionOf(isPattern);
		if (condition is IConditionalOperation { Syntax: ConditionalExpressionSyntax })
		{
			Report(context, isPattern, "Match, Map(…).GetValueOr(…) or ToNullable()", "a case pattern in a conditional expression");
			return;
		}

		if (condition is null)
		{
			Report(
				context,
				isPattern,
				optional ? "IsSome(), IsNone() or Match(p, () => false)" : "IsSuccess(), IsError() or Match(p, _ => false)",
				"a case pattern used as a bool"
			);
			return;
		}

		if (condition is IConditionalOperation statement && (Razor.IsMarkup(isPattern, context.ContainingSymbol) || IsAllowedGuard(statement, isPattern)))
		{
			return;
		}

		if (condition is IConditionalOperation guard && LeavesLoop(guard))
		{
			Report(context, isPattern, optional ? "IsNone() or IsSome()" : "IsError() or IsSuccess()", "a case pattern that binds nothing in a loop guard");
			return;
		}

		if (condition is IConditionalOperation onlySuccess && IsSuccessOnlyLoop(onlySuccess, isPattern, monads))
		{
			Report(context, isPattern, "WhereSuccessAwait()", "an await foreach that only acts on Success elements");
			return;
		}

		Report(context, isPattern, optional ? "Apply, ApplyNone or Match" : "Apply, ApplyError or Match", "a branch on a case");
	}

	private static void AnalyzeSwitch(OperationAnalysisContext context, IOperation value, IEnumerable<IPatternOperation> patterns, MonadTypes monads)
	{
		if (!monads.IsMonad(value.Type) && !patterns.Any(pattern => MonadPatterns(pattern, monads).Count > 0))
		{
			return;
		}

		Report(context, value, "Match or Map(…).GetValueOr(…)", "a switch over the cases");
	}

	private static void AnalyzeConstruction(OperationAnalysisContext context, IConditionalOperation conditional, MonadTypes monads)
	{
		if (conditional.Syntax is not ConditionalExpressionSyntax syntax || conditional.WhenFalse is null)
		{
			return;
		}

		var branches = new[] { MonadTypes.Unwrap(conditional.WhenTrue), MonadTypes.Unwrap(conditional.WhenFalse) };
		var condition = MonadTypes.Unwrap(conditional.Condition);
		if (!branches.Any(IsNone) || ReadsMonad(condition, monads))
		{
			return;
		}

		var kept = branches.First(branch => !IsNone(branch));
		if (IsSomeOf(kept))
		{
			AnalyzeSomeOrNone(context, conditional, kept, condition, syntax.Condition);
			return;
		}

		if (IsPlainRead(kept) && monads.IsOptional(kept.Type) && Defers(kept, syntax.Condition))
		{
			Report(context, conditional, "When(…) on the Optional", "a conditional that picks an Optional or new None()");
		}
	}

	private static void AnalyzeSomeOrNone(
		OperationAnalysisContext context,
		IOperation conditional,
		IOperation some,
		IOperation condition,
		ExpressionSyntax conditionSyntax
	)
	{
		if (IsNullTest(condition))
		{
			Report(context, conditional, "Optional.Of(…)", "a conditional that builds Some.Of or new None() from a null test");
			return;
		}

		if (!Defers(some, conditionSyntax))
		{
			return;
		}

		if (IsDictionaryLookup(condition))
		{
			Report(context, conditional, "GetValueOrNone(…)", "a conditional that builds Some.Of or new None() from TryGetValue");
			return;
		}

		var use = HoldsNullTest(condition) ? "Optional.Of(…).When(…)" : "Optional.When(…)";
		Report(context, conditional, use, "a conditional that builds Some.Of or new None()");
	}

	private static bool ReadsMonad(IOperation condition, MonadTypes monads)
	{
		return condition.DescendantsAndSelf().Any(operation => monads.IsMonad(operation.Type));
	}

	private static bool Defers(IOperation value, ExpressionSyntax condition)
	{
		var flow = value.SemanticModel?.AnalyzeDataFlow(condition);
		if (flow is not { Succeeded: true })
		{
			return false;
		}

		var unassigned = flow.VariablesDeclared.Except(flow.AlwaysAssigned, SymbolEqualityComparer.Default).ToList();
		return !value.DescendantsAndSelf().Any(operation => Pinned(operation, unassigned));
	}

	private static bool Pinned(IOperation operation, IReadOnlyList<ISymbol> unassigned)
	{
		return operation switch
		{
			IAwaitOperation => true,
			ILocalReferenceOperation local => local.Local.IsRef || local.Local.Type.IsRefLikeType || unassigned.Contains(local.Local, SymbolEqualityComparer.Default),
			IParameterReferenceOperation parameter => parameter.Parameter.RefKind != RefKind.None || parameter.Parameter.Type.IsRefLikeType,
			IInstanceReferenceOperation instance => instance.Type?.IsValueType == true,
			_ => false,
		};
	}

	private static bool IsDictionaryLookup(IOperation condition)
	{
		return condition is IInvocationOperation { TargetMethod: { Name: "TryGetValue", IsGenericMethod: false } method }
			&& method.ContainingType.AllInterfaces.Add(method.ContainingType).Any(IsDictionary);
	}

	private static bool IsDictionary(INamedTypeSymbol type)
	{
		return type.MetadataName is "IReadOnlyDictionary`2" or "IDictionary`2" && type.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";
	}

	private static bool HoldsNullTest(IOperation condition)
	{
		return condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } both
			&& new[] { MonadTypes.Unwrap(both.LeftOperand), MonadTypes.Unwrap(both.RightOperand) }.Any(side => IsNullTest(side) || HoldsNullTest(side));
	}

	private static bool IsPlainRead(IOperation operation)
	{
		return operation is ILocalReferenceOperation or IParameterReferenceOperation or IFieldReferenceOperation or IPropertyReferenceOperation;
	}

	private static bool PicksNull(IInvocationOperation invocation)
	{
		if (
			invocation.Arguments.Length != 1
			|| MonadTypes.Unwrap(invocation.Arguments[0].Value)
				is not IConditionalOperation { WhenFalse: { } whenFalse, Syntax: ConditionalExpressionSyntax syntax } picked
		)
		{
			return false;
		}

		var kept = Kept(picked.WhenTrue, whenFalse);
		return kept is not null && !IsNullTest(MonadTypes.Unwrap(picked.Condition)) && IsNotNull(kept) && Defers(kept, syntax.Condition);
	}

	private static IOperation? Kept(IOperation whenTrue, IOperation whenFalse)
	{
		if (IsNullArm(whenFalse))
		{
			return whenTrue;
		}

		return IsNullArm(whenTrue) ? whenFalse : null;
	}

	private static bool IsNullArm(IOperation operation)
	{
		return Stripped(operation) is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } } or IDefaultValueOperation;
	}

	private static bool IsNotNull(IOperation operation)
	{
		var value = Stripped(operation);
		if (value.Type is { IsValueType: true } type)
		{
			return type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
		}

		return value.SemanticModel?.GetTypeInfo(value.Syntax).Nullability.FlowState == NullableFlowState.NotNull;
	}

	private static IOperation Stripped(IOperation operation)
	{
		var current = operation;
		while (current is IConversionOperation or IParenthesizedOperation)
		{
			current = current switch
			{
				IConversionOperation conversion => conversion.Operand,
				IParenthesizedOperation parenthesized => parenthesized.Operand,
				_ => current,
			};
		}

		return current;
	}

	private static void AnalyzeInvocation(OperationAnalysisContext context, IInvocationOperation invocation, MonadTypes monads)
	{
		var method = invocation.TargetMethod;
		if (MonadTypes.IsHelper(method, Steps))
		{
			foreach (var lambda in Lambdas(invocation))
			{
				AnalyzeStep(context, method.Name, lambda);
			}

			return;
		}

		if (OrderingKeys.Contains(method.Name))
		{
			if (Lambdas(invocation).Any(lambda => monads.IsMonad(lambda.Symbol.ReturnType)))
			{
				Report(context, invocation, ".ToNullable() in the key", "an Optional or a Result as an ordering key");
			}

			return;
		}

		if (method.ContainingType?.Name == "Optional" && MonadTypes.IsHelper(method, "Of") && PicksNull(invocation))
		{
			Report(context, invocation, "Optional.When(…)", "Optional.Of over a conditional that picks null");
			return;
		}

		if (method.Name is "AddContent" && method.ContainingType?.Name == "RenderTreeBuilder")
		{
			foreach (var argument in invocation.Arguments.Where(argument => argument.Parameter?.Name == "textContent"))
			{
				AnalyzeText(context, argument.Value, monads);
			}
		}
	}

	private static void AnalyzeStep(OperationAnalysisContext context, string helper, IAnonymousFunctionOperation lambda)
	{
		var statements = lambda.Body.Operations.Where(operation => !operation.IsImplicit).ToList();
		if (statements.Count > 1)
		{
			Report(context, lambda, "one chained call per step, or a named method for a step that needs several statements", $"a block lambda passed to {helper}");
			return;
		}

		if (lambda.Symbol.ReturnsVoid && lambda.Body.Operations.FirstOrDefault() is IExpressionStatementOperation { Operation.Type: { } type } && IsAwaitable(type))
		{
			Report(context, lambda, "async x => await StepAsync(x)", $"a {type.Name}-returning step bound to {helper}'s Action overload, which is never awaited");
		}
	}

	private static void AnalyzeText(OperationAnalysisContext context, IOperation value, MonadTypes monads)
	{
		var text = MonadTypes.Unwrap(value);
		if (monads.IsMonad(text.Type))
		{
			Report(context, text, "GetValueOr(…) or Match(…)", "an Optional or a Result written as text");
		}
	}

	private static void AnalyzeNullConversion(OperationAnalysisContext context, IConversionOperation conversion, MonadTypes monads)
	{
		if (!monads.IsMonad(conversion.Type))
		{
			return;
		}

		var operand = conversion.Operand;
		while (operand is IConversionOperation inner)
		{
			operand = inner.Operand;
		}

		if (operand is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
		{
			Report(context, conversion, Missing(monads, conversion.Type), "a null literal, which builds a value holding no case");
		}
	}

	private static string Missing(MonadTypes monads, ITypeSymbol? type)
	{
		return monads.IsOptional(type) ? "new None()" : "a Success or an Error";
	}

	private static List<IPatternOperation> MonadPatterns(IPatternOperation root, MonadTypes monads)
	{
		return
		[
			.. root.DescendantsAndSelf()
				.OfType<IPatternOperation>()
				.Where(pattern => monads.IsMonad(pattern.InputType))
				.Where(pattern => pattern is not (IDiscardPatternOperation or IDeclarationPatternOperation { MatchesNull: true })),
		];
	}

	private static bool IsShapeTest(IPatternOperation pattern, MonadTypes monads)
	{
		return pattern switch
		{
			IConstantPatternOperation => true,
			IRecursivePatternOperation recursive => monads.IsMonad(recursive.MatchedType),
			ITypePatternOperation type => monads.IsMonad(type.MatchedType),
			IDeclarationPatternOperation declaration => monads.IsMonad(declaration.MatchedType),
			_ => false,
		};
	}

	private static IOperation? ConditionOf(IIsPatternOperation isPattern)
	{
		IOperation current = isPattern;
		while (
			current.Parent
				is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not }
					or IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr }
					or IParenthesizedOperation
		)
		{
			current = current.Parent;
		}

		return current.Parent switch
		{
			IConditionalOperation conditional when conditional.Condition == current => conditional,
			IWhileLoopOperation loop when loop.Condition == current => loop,
			_ => null,
		};
	}

	private static bool IsAllowedGuard(IConditionalOperation statement, IIsPatternOperation isPattern)
	{
		if (statement.WhenFalse is not null || MonadTypes.Unwrap(statement.Condition) != isPattern)
		{
			return false;
		}

		if (isPattern.Pattern is not INegatedPatternOperation { Pattern: var bound } || !BindsSuccess(bound))
		{
			return false;
		}

		if (LeavesLoop(statement))
		{
			return true;
		}

		return Last(statement.WhenTrue) is IReturnOperation && Following(statement).Any(operation => operation is IUsingDeclarationOperation or ITryOperation);
	}

	private static bool BindsSuccess(IPatternOperation pattern)
	{
		var matched = pattern switch
		{
			IRecursivePatternOperation recursive => recursive.MatchedType,
			IDeclarationPatternOperation declaration => declaration.MatchedType,
			_ => null,
		};

		return matched?.Name is "Some" or "Success"
			&& pattern.DescendantsAndSelf().OfType<IDeclarationPatternOperation>().Any(declaration => declaration.DeclaredSymbol is not null);
	}

	private static bool LeavesLoop(IConditionalOperation statement)
	{
		if (statement.WhenFalse is not null)
		{
			return false;
		}

		return Last(statement.WhenTrue) switch
		{
			IBranchOperation { BranchKind: BranchKind.Continue } => true,
			IBranchOperation { BranchKind: BranchKind.Break } => NearestBreakable(statement) is ILoopOperation,
			_ => false,
		};
	}

	private static IOperation? NearestBreakable(IOperation operation)
	{
		for (var current = operation.Parent; current is not null and not IAnonymousFunctionOperation and not ILocalFunctionOperation; current = current.Parent)
		{
			if (current is ILoopOperation or ISwitchOperation)
			{
				return current;
			}
		}

		return null;
	}

	private static bool IsSuccessOnlyLoop(IConditionalOperation statement, IIsPatternOperation isPattern, MonadTypes monads)
	{
		return statement.WhenFalse is null
			&& BindsSuccess(isPattern.Pattern)
			&& statement.Parent is IBlockOperation { Operations.Length: 1, Parent: IForEachLoopOperation { IsAsynchronous: true } loop }
			&& loop.LoopControlVariable is IVariableDeclaratorOperation declarator
			&& monads.IsResult(declarator.Symbol.Type);
	}

	private static IOperation Last(IOperation operation)
	{
		return operation is IBlockOperation { Operations.Length: > 0 } block ? block.Operations[block.Operations.Length - 1] : operation;
	}

	private static IEnumerable<IOperation> Following(IOperation statement)
	{
		return statement.Parent is IBlockOperation block ? block.Operations.SkipWhile(operation => operation != statement).Skip(1) : [];
	}

	private static IEnumerable<IPatternOperation> CasePatterns(ISwitchOperation switchStatement)
	{
		return switchStatement.Cases.SelectMany(@case => @case.Clauses).OfType<IPatternCaseClauseOperation>().Select(clause => clause.Pattern);
	}

	private static IEnumerable<IAnonymousFunctionOperation> Lambdas(IInvocationOperation invocation)
	{
		return invocation
			.Arguments.Select(argument => MonadTypes.Unwrap(argument.Value))
			.OfType<IDelegateCreationOperation>()
			.Select(creation => creation.Target)
			.OfType<IAnonymousFunctionOperation>();
	}

	private static bool IsSomeOf(IOperation operation)
	{
		return operation switch
		{
			IInvocationOperation { TargetMethod: { Name: "Of", ContainingType.Name: "Some" } } => true,
			IObjectCreationOperation { Type.Name: "Some" } => true,
			_ => false,
		};
	}

	private static bool IsNone(IOperation operation)
	{
		return operation is IObjectCreationOperation { Type.Name: "None" } or IDefaultValueOperation { Type.Name: "None" };
	}

	private static bool IsNullTest(IOperation condition)
	{
		return condition switch
		{
			IIsPatternOperation isPattern => IsNullPattern(isPattern.Pattern),
			IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } binary => IsNull(binary.LeftOperand)
				|| IsNull(binary.RightOperand),
			IPropertyReferenceOperation { Property.Name: "HasValue" } => true,
			IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not => IsNullTest(MonadTypes.Unwrap(not.Operand)),
			_ => false,
		};
	}

	private static bool IsNullPattern(IPatternOperation pattern)
	{
		return pattern switch
		{
			IConstantPatternOperation constant => IsNull(constant.Value),
			INegatedPatternOperation negated => IsNullPattern(negated.Pattern),
			IRecursivePatternOperation { PropertySubpatterns.Length: 0, DeconstructionSubpatterns.Length: 0 } => true,
			_ => false,
		};
	}

	private static bool IsNull(IOperation operation)
	{
		return MonadTypes.Unwrap(operation) is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } };
	}

	private static bool IsAwaitable(ITypeSymbol type)
	{
		return type.Name is "Task" or "ValueTask" && type.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks";
	}

	private static void Report(OperationAnalysisContext context, IOperation operation, string use, string instead)
	{
		context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), use, instead));
	}
}
