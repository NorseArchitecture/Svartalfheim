using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Norse.Architecture.Analyzers;

/// <summary>
/// NORSE074 — forced document load outside the seam's implementation. A forced reload exists to
/// re-establish a session under a changed principal, and only the gate changes the principal, so the
/// only absolved call site is ForceLoadSessionTransition itself — matched by BOTH its full type name
/// and its assembly, so no other assembly can mint the name and the gate's own pages are convicted
/// like everyone else (an assembly-wide exemption would be the rejected interface opt-out at assembly
/// blast radius). Enforcement is fail-loud: anything not provably soft convicts — a non-constant
/// forceLoad argument, or an options value the analyzer cannot read inline. Runs over generated code
/// deliberately: .razor components compile to auto-generated C#, and the default None would blind
/// the rule to every Razor call site.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForcedLoadAnalyzer : DiagnosticAnalyzer
{
	const string
		GateAssembly = "Norse.AuthN.Components",
		ImplementationType = "Norse.AuthN.Components.ForceLoadSessionTransition";

	static readonly ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics =
		[Diagnostics.ForcedLoadOutsideTheGate];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		_supportedDiagnostics;

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(static start =>
		{
			if (RealmIdentity.IsExempt(start.Compilation.AssemblyName ?? ""))
				return;
			start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
		});
	}

	static void AnalyzeInvocation(OperationAnalysisContext context)
	{
		var invocation = (IInvocationOperation)context.Operation;
		if (invocation.TargetMethod.Name != "NavigateTo"
			|| invocation.TargetMethod.ContainingType.ToDisplayString() != "Microsoft.AspNetCore.Components.NavigationManager")
			return;

		// The one absolved call site: the seam's own implementation — type name AND assembly, both.
		// A second type of this name cannot compile in the gate assembly; the name minted anywhere
		// else fails the assembly key.
		if (context.Compilation.AssemblyName == GateAssembly
			&& context.ContainingSymbol.ContainingType?.ToDisplayString() == ImplementationType)
			return;

		if (!invocation.Arguments.Any(IsForced))
			return;

		context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ForcedLoadOutsideTheGate,
			invocation.Syntax.GetLocation(), context.ContainingSymbol.ToDisplayString()));
	}

	static bool IsForced(IArgumentOperation argument) =>
		argument.Parameter?.Name switch
		{
			// Anything not provably the constant false convicts — variables, negations, method
			// results. The omitted-argument default arrives as a constant false and stays clean.
			"forceLoad" =>
				argument.Value.ConstantValue is not { HasValue: true, Value: false },
			// The options overload demands an inline initializer the analyzer can read: a prebuilt
			// options value convicts outright; an inline initializer convicts unless ForceLoad is
			// absent or provably the constant false.
			"options" =>
				argument.Value is not IObjectCreationOperation creation ||
				creation.Initializer?.Initializers
					.OfType<ISimpleAssignmentOperation>()
					.FirstOrDefault(static assignment =>
						assignment.Target is IPropertyReferenceOperation { Property.Name: "ForceLoad" })
					is { } forceLoad
					&& forceLoad.Value.ConstantValue is not { HasValue: true, Value: false },
			_ => false
		};
}
