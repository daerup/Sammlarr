using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Options;

[Generator]
public class OptionsSectionRegistrationGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(static postInit =>
			AddGeneratedSource(postInit, OptionsSectionInterfaceSource.FileName, OptionsSectionInterfaceSource.Source)
		);

		var candidateTypes = context
			.SyntaxProvider.CreateSyntaxProvider(static (node, _) => IsCandidateTypeDeclaration(node), static (ctx, _) => GetCandidateType(ctx))
			.Where(static type => type is not null)
			.Collect();

		context.RegisterSourceOutput(context.CompilationProvider.Combine(candidateTypes), static (spc, source) => Execute(spc, source.Left, source.Right));
	}

	private static bool IsCandidateTypeDeclaration(SyntaxNode node) => node is TypeDeclarationSyntax;

	private static INamedTypeSymbol? GetCandidateType(GeneratorSyntaxContext context) =>
		context.Node is TypeDeclarationSyntax typeDeclaration ? context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol : null;

	private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<INamedTypeSymbol?> candidateTypes)
	{
		var markerInterface = compilation.GetTypeByMetadataName(OptionsSectionInterfaceSource.MetadataName);
		if (markerInterface is null)
		{
			return;
		}

		var source = OptionsRegistrationSource.Generate(candidateTypes, markerInterface);
		if (source is not null)
		{
			AddGeneratedSource(context, OptionsRegistrationSource.FileName, source);
		}
	}

	private static void AddGeneratedSource(IncrementalGeneratorPostInitializationContext context, string fileName, string source) =>
		context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));

	private static void AddGeneratedSource(SourceProductionContext context, string fileName, string source) =>
		context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
}
