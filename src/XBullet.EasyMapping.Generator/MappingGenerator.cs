using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XBullet.EasyMapping.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class MappingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var approvals = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(ModelApprovalSnapshot.Suffix, StringComparison.Ordinal))
            .Select(static (text, cancellationToken) => new ModelApprovalFile(text.Path, text.GetText(cancellationToken)?.ToString()))
            .Collect();
        var mappers = context.SyntaxProvider.ForAttributeWithMetadataName(
            "XBullet.EasyMapping.MapperAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, cancellationToken) =>
                new MappingPlanner(attributeContext.SemanticModel.Compilation,
                    (INamedTypeSymbol)attributeContext.TargetSymbol, cancellationToken).Build())
            .WithTrackingName("MapperPlan")
            .Combine(approvals)
            .Select(static (pair, _) => ModelApprovalVerifier.Verify(pair.Left, pair.Right))
            .WithTrackingName("MapperApproval");

        context.RegisterSourceOutput(mappers, static (productionContext, mapper) =>
        {
            foreach (var diagnostic in mapper.Diagnostics)
            {
                productionContext.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (mapper.Source.Length > 0)
            {
                productionContext.AddSource(mapper.HintName, SourceText.From(mapper.Source, Encoding.UTF8));
            }
            if (mapper.Approval is { } approval)
            {
                productionContext.AddSource(approval.SourceHint, SourceText.From(approval.GenerateSource(), Encoding.UTF8));
            }
        });
    }
}
