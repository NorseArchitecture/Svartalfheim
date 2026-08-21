using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Norse.Architecture.Analyzers;

/// <summary>
///     NORSE013 — strikes every authored construct that adds <c>IAllowAnonymous</c> endpoint metadata.
///     Two shapes reach that metadata and both are covered: an attribute implementing
///     <c>IAllowAnonymous</c> (<c>[AllowAnonymous]</c> and any custom one), and the framework's fluent
///     <c>.AllowAnonymous()</c> convention-builder extension.
/// </summary>
/// <remarks>
///     Both halves are matched <b>semantically</b>, not by name. The attribute test is
///     "implements <c>IAllowAnonymous</c>", so a custom marker attribute cannot slip past exact-type
///     equality; the invocation test is "the framework's extension in
///     <c>Microsoft.AspNetCore.Builder</c> constrained to <c>IEndpointConventionBuilder</c>", so a user's
///     own method that happens to be called <c>AllowAnonymous</c> is not convicted for its name. Matching
///     on the name alone produces false positives and exact-attribute-equality produces false negatives —
///     from the same imprecision, in opposite directions.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AllowAnonymousAnalyzer : DiagnosticAnalyzer
{
	const string MarkerMetadataName = "Microsoft.AspNetCore.Authorization.IAllowAnonymous";
	const string BuilderMetadataName = "Microsoft.AspNetCore.Builder.IEndpointConventionBuilder";
	const string ExtensionsNamespace = "Microsoft.AspNetCore.Builder";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		[Diagnostics.AllowAnonymousBanned];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(start =>
		{
			var marker = start.Compilation.GetTypeByMetadataName(MarkerMetadataName);
			if (marker is null)
				return;

			var conventionBuilder = start.Compilation.GetTypeByMetadataName(BuilderMetadataName);

			start.RegisterSymbolAction(symbol => InspectAttributes(symbol, marker),
				SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Property);

			if (conventionBuilder is not null)
				start.RegisterOperationAction(operation => InspectInvocation(operation, conventionBuilder),
					OperationKind.Invocation);
		});
	}

	static void InspectAttributes(SymbolAnalysisContext context, INamedTypeSymbol marker)
	{
		foreach (var data in context.Symbol.GetAttributes())
		{
			// "Implements the marker", not "is AllowAnonymousAttribute" -- the law is about the metadata an
			// attribute contributes, and a custom attribute implementing IAllowAnonymous contributes exactly
			// the same thing.
			if (data.AttributeClass is not { } applied
				|| !applied.AllInterfaces.Contains(marker, SymbolEqualityComparer.Default))
				continue;
			if (data.ApplicationSyntaxReference?.GetSyntax() is not { } syntax)
				continue;

			context.ReportDiagnostic(Diagnostic.Create(
				Diagnostics.AllowAnonymousBanned, syntax.GetLocation(), context.Symbol.Name));
		}
	}

	static void InspectInvocation(OperationAnalysisContext context, INamedTypeSymbol conventionBuilder)
	{
		var invocation = (IInvocationOperation)context.Operation;
		var method = invocation.TargetMethod;

		if (method.Name != "AllowAnonymous"
			|| !method.IsExtensionMethod
			|| method.ContainingType?.ContainingNamespace?.ToDisplayString() != ExtensionsNamespace)
			return;

		// The receiver must be a convention builder. A user extension on string, or on their own type, is
		// none of this rule's business no matter what it is called.
		var receiver = method.ReducedFrom?.Parameters.FirstOrDefault()?.Type ?? method.Parameters
			.FirstOrDefault()?.Type;
		if (receiver is null || !SatisfiesConventionBuilder(receiver, conventionBuilder))
			return;

		context.ReportDiagnostic(Diagnostic.Create(
			Diagnostics.AllowAnonymousBanned,
			invocation.Syntax.GetLocation(),
			method.ContainingType!.Name));
	}

	static bool SatisfiesConventionBuilder(ITypeSymbol receiver, INamedTypeSymbol conventionBuilder) =>
		receiver switch
		{
			// The framework declares it as AllowAnonymous<TBuilder>(this TBuilder) where TBuilder :
			// IEndpointConventionBuilder, so at the call site the receiver is a type parameter with that
			// constraint rather than the interface itself.
			ITypeParameterSymbol parameter =>
				parameter.ConstraintTypes.Any(t =>
					SymbolEqualityComparer.Default.Equals(t, conventionBuilder)),
			_ => SymbolEqualityComparer.Default.Equals(receiver, conventionBuilder)
				|| receiver.AllInterfaces.Contains(conventionBuilder, SymbolEqualityComparer.Default)
		};
}
