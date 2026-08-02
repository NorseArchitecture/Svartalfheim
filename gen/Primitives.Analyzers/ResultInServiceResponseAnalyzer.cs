using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Norse.Primitives.Analyzers;

/// <summary>
/// NORSE060 — <c>Result&lt;T&gt;</c> must never appear as a property type reachable from a
/// <c>[ServiceContract]</c> interface's <c>[OperationContract]</c> method's response payload. Fires in
/// ANY compilation that declares such an interface, regardless of whether a REST/XML facade exists
/// anywhere near it — closing the gap Midgard's narrower NORSE023 leaves open (that diagnostic only
/// fires for types reachable from a <c>GrpcControllerBase</c>-derived facade controller's action
/// signature, in the one host compilation that happens to expose that controller; most
/// <c>[ServiceContract]</c> services on this platform carry no such facade at all). Bundled into every
/// consumer of Norse.Primitives by construction — <c>Result&lt;T&gt;</c> lives in this package, so
/// anyone who can even declare a <c>Result&lt;T&gt;</c>-typed property already carries this analyzer,
/// with zero opt-in required anywhere else on the platform.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultInServiceResponseAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Diagnostics.ResultInServiceResponse];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(OnCompilationStart);
	}

	static void OnCompilationStart(CompilationStartAnalysisContext context)
	{
		if (WellKnownTypes.Resolve(context.Compilation) is not WellKnownTypes wellKnown)
			return;

		context.RegisterSymbolAction(c => AnalyzeInterface(c, wellKnown), SymbolKind.NamedType);
	}

	static void AnalyzeInterface(SymbolAnalysisContext context, WellKnownTypes wellKnown)
	{
		if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Interface } serviceInterface)
			return;

		if (!HasAttribute(serviceInterface, wellKnown.ServiceContractAttribute))
			return;

		foreach (var operation in serviceInterface.GetMembers().OfType<IMethodSymbol>())
			if (HasAttribute(operation, wellKnown.OperationContractAttribute))
				ResponseClosureWalker.AnalyzeOperation(context, serviceInterface, operation, wellKnown);
	}

	static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attribute) =>
		symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));
}
