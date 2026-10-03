using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Roslyn.Monads;

internal static class Razor
{
	private const string Builder = "RenderTreeBuilder";

	internal static bool IsGenerated(SyntaxTree tree)
	{
		return tree.FilePath.EndsWith("_razor.g.cs", StringComparison.OrdinalIgnoreCase)
			|| tree.FilePath.EndsWith(".razor.g.cs", StringComparison.OrdinalIgnoreCase);
	}

	internal static bool IsMarkup(IOperation operation, ISymbol? containing)
	{
		if (!IsGenerated(operation.Syntax.SyntaxTree))
		{
			return false;
		}

		for (var current = operation.Parent; current is not null; current = current.Parent)
		{
			switch (current)
			{
				case IAnonymousFunctionOperation lambda:
					return lambda.Symbol.Parameters.Length == 1 && lambda.Symbol.Parameters[0].Type.Name == Builder;
				case ILocalFunctionOperation:
					return false;
			}
		}

		return containing is IMethodSymbol { Name: "BuildRenderTree", Parameters.Length: 1 } method && method.Parameters[0].Type.Name == Builder;
	}
}
