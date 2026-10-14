using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Roslyn.Monads;

internal sealed class MonadTypes
{
	private const string SharedNamespace = "Shared";

	private readonly INamedTypeSymbol? _optional;
	private readonly INamedTypeSymbol? _result;
	private readonly INamedTypeSymbol? _task;
	private readonly INamedTypeSymbol? _valueTask;

	private MonadTypes(Compilation compilation)
	{
		_optional = compilation.GetTypeByMetadataName("Shared.Optional`1");
		_result = compilation.GetTypeByMetadataName("Shared.Result`1");
		_task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
		_valueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
	}

	internal bool Present => _optional is not null || _result is not null;

	internal static MonadTypes For(Compilation compilation) => new(compilation);

	internal static bool IsShared(Compilation compilation) => compilation.AssemblyName == SharedNamespace;

	internal bool IsMonad(ITypeSymbol? type) => IsOptional(type) || IsResult(type);

	internal bool IsOptional(ITypeSymbol? type) => Is(type, _optional);

	internal bool IsResult(ITypeSymbol? type) => Is(type, _result);

	internal bool IsAwaitedMonad(ITypeSymbol? type)
	{
		return type is INamedTypeSymbol { TypeArguments.Length: 1 } named && (Is(named, _task) || Is(named, _valueTask)) && IsMonad(named.TypeArguments[0]);
	}

	internal static bool IsHelper(IMethodSymbol method, params string[] names)
	{
		return names.Contains(method.Name, StringComparer.Ordinal) && InSharedNamespace(method.ContainingType);
	}

	internal static IOperation? Receiver(IInvocationOperation invocation)
	{
		if (invocation.Instance is { } instance)
		{
			return instance;
		}

		return invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null;
	}

	internal static IOperation Unwrap(IOperation operation)
	{
		var current = operation;
		while (current is IConversionOperation { IsImplicit: true } or IParenthesizedOperation)
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

	private static bool InSharedNamespace(INamedTypeSymbol? type)
	{
		var current = type;
		while (current is { ContainingType: { } outer })
		{
			current = outer;
		}

		return current?.ContainingNamespace?.ToDisplayString() == SharedNamespace;
	}

	private static bool Is(ITypeSymbol? type, INamedTypeSymbol? monad)
	{
		return monad is not null && type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, monad);
	}
}
