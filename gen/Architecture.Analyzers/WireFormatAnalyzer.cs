using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Norse.Architecture.Analyzers;

/// <summary>
/// NORSE070 (spec §1 Law #1, §4): anything naming or executing a concrete encoding exists in
/// Infrastructure/Hosting alone. Brand-blind — evaluated on the function segments of the compilation's
/// assembly name, so an anchorless .Contracts assembly with zero governed references is still governed.
/// Three layers: using directives (aliases and global usings included), qualified names, and
/// banned-symbol operations so alias-laundered use still strikes. Contract attributes are blessed by
/// construction: System.Runtime.Serialization and System.ServiceModel are not on the banned-root list —
/// only their serializer machinery is, by symbol.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WireFormatAnalyzer : DiagnosticAnalyzer
{
	static readonly ImmutableArray<string> _bannedRoots =
	[
		"System.Text.Json", "Newtonsoft.Json", "System.Xml", "System.Runtime.Serialization.Json",
		"System.Net.Http.Json", "Microsoft.AspNetCore.Http.Json", "ProtoBuf", "Grpc", "Google.Protobuf", "MessagePack"
	];

	// (containing type metadata name, member name or null-for-any-instantiation)
	static readonly ImmutableArray<(string Type, string? Member)> _bannedSymbols =
	[
		("System.Runtime.Serialization.DataContractSerializer", null),
		("System.Runtime.Serialization.XmlObjectSerializer", null),
		("Microsoft.AspNetCore.Http.Results", "Json"),
		("Microsoft.AspNetCore.Http.TypedResults", "Json")
	];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[Diagnostics.WireFormatOutsideBorder];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics); // Ruled 2026-08-03: generator output compiles into governed assemblies, so Law #1 governs it — the gen/ exemption covers the generator assembly, not its emissions.
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(static start =>
		{
			var name = start.Compilation.AssemblyName ?? "";
			if (RealmIdentity.IsExempt(name) || RealmIdentity.IsWireBorder(name))
				return;
			start.RegisterSyntaxNodeAction(AnalyzeUsing, SyntaxKind.UsingDirective);
			start.RegisterSyntaxNodeAction(AnalyzeQualifiedName, SyntaxKind.QualifiedName);
			start.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation, OperationKind.ObjectCreation);
		});
	}

	static void AnalyzeUsing(SyntaxNodeAnalysisContext context)
	{
		var directive = (UsingDirectiveSyntax)context.Node;
		var name = directive.Name?.ToString();
		if (name is not null && MatchesBannedRoot(name))
			Report(context, directive.GetLocation(), name);
	}

	static void AnalyzeQualifiedName(SyntaxNodeAnalysisContext context)
	{
		// Using directives are handled (and reported once) above; skip their interior nodes. Also skip
		// inner QualifiedName nodes whose parent is a longer QualifiedName — only the outermost reports.
		// Additionally, skip when the qualified name is the type of an ObjectCreationExpressionSyntax —
		// the operation layer owns that report and will fire once; skipping here dedupes.
		var node = (QualifiedNameSyntax)context.Node;
		if (node.Parent is QualifiedNameSyntax or ObjectCreationExpressionSyntax || node.FirstAncestorOrSelf<UsingDirectiveSyntax>() is not null)
			return;
		var text = node.ToString();
		if (MatchesBannedRoot(text))
			Report(context, node.GetLocation(), text);
	}

	static void AnalyzeOperation(OperationAnalysisContext context)
	{
		var (symbol, location) = context.Operation switch
		{
			IInvocationOperation invocation => ((ISymbol)invocation.TargetMethod, invocation.Syntax.GetLocation()),
			IObjectCreationOperation { Constructor: { } ctor } creation => (ctor, creation.Syntax.GetLocation()),
			_ => (null!, null!)
		};
		if (symbol is null)
			return;
		var containingType = symbol.ContainingType?.ToDisplayString();
		var containingNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? "";
		var banned =
			MatchesBannedRoot(containingNamespace) ||
			_bannedSymbols.Any(b => b.Type == containingType && (b.Member is null || b.Member == symbol.Name));
		if (banned)
			context.ReportDiagnostic(Diagnostic.Create(Diagnostics.WireFormatOutsideBorder, location, $"{containingType}.{symbol.Name}"));
	}

	static bool MatchesBannedRoot(string name) =>
		_bannedRoots.Any(root =>
			name.StartsWith(root, StringComparison.Ordinal) &&
			(name.Length == root.Length || name[root.Length] == '.'));

	static void Report(SyntaxNodeAnalysisContext context, Location location, string offender) =>
		context.ReportDiagnostic(Diagnostic.Create(Diagnostics.WireFormatOutsideBorder, location, offender));
}
