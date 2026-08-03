using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Norse.Architecture.Analyzers;

/// <summary>
/// NORSE079 — the meta-strike. NotConfigurable closes the severity channel, but SuppressMessageAttribute
/// rides another and erases even Location.None strikes; nothing defeats attribute suppression, so the
/// law convicts the attempt instead. Purely syntactic — no semantic model, so a suppression targeting a
/// misspelled or nonexistent id still strikes; and because the check is syntactic, suppressing NORSE079
/// itself is just another matching attribute, re-convicted recursively.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuppressionLawAnalyzer : DiagnosticAnalyzer
{
	static readonly ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics =
		[Diagnostics.SuppressingTheLaw];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		_supportedDiagnostics;

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(static start =>
		{
			var name = start.Compilation.AssemblyName ?? "";
			if (RealmIdentity.IsExempt(name))
				return;
			start.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
		});
	}

	static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
	{
		var attribute = (AttributeSyntax)context.Node;
		if (RightmostIdentifier(attribute.Name) is not ("SuppressMessage" or "SuppressMessageAttribute"))
			return;

		var matched = attribute.ArgumentList?.Arguments
			.Select(a => a.Expression)
			.OfType<LiteralExpressionSyntax>()
			.Where(l => l.IsKind(SyntaxKind.StringLiteralExpression))
			.Select(l => l.Token.ValueText)
			.FirstOrDefault(v => v.StartsWith("NORSE07", StringComparison.Ordinal));
		if (matched is not null)
			context.ReportDiagnostic(Diagnostic.Create(Diagnostics.SuppressingTheLaw, attribute.GetLocation(), matched));
	}

	static string RightmostIdentifier(NameSyntax name) =>
		name switch
		{
			QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
			SimpleNameSyntax simple => simple.Identifier.Text,
			_ => name.ToString()
		};
}
