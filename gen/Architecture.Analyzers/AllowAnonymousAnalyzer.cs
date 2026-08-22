using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Norse.Architecture.Analyzers;

/// <summary>
///     NORSE013 — strikes every authored construct that adds <c>IAllowAnonymous</c> endpoint metadata.
///     Three shapes reach that metadata and all three are covered: an attribute implementing
///     <c>IAllowAnonymous</c> (<c>[AllowAnonymous]</c> and any custom one, on a named member or directly
///     on a minimal-API lambda handler), the framework's fluent <c>.AllowAnonymous()</c>
///     convention-builder extension, and a marker instance added directly via <c>.WithMetadata(...)</c>.
/// </summary>
/// <remarks>
///     All three halves are matched <b>semantically</b>, not by name. The attribute test is
///     "implements <c>IAllowAnonymous</c>", so a custom marker attribute cannot slip past exact-type
///     equality; the invocation tests are "the framework's own extension method" — identified by its
///     declaring type, not by namespace string — constrained to <c>IEndpointConventionBuilder</c>, so a
///     third party's own method that happens to be called <c>AllowAnonymous</c> or <c>WithMetadata</c> is
///     not convicted for its name. Matching on the name alone produces false positives and
///     exact-attribute-equality produces false negatives — from the same imprecision, in opposite
///     directions.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AllowAnonymousAnalyzer : DiagnosticAnalyzer
{
	const string MarkerMetadataName = "Microsoft.AspNetCore.Authorization.IAllowAnonymous";
	const string BuilderMetadataName = "Microsoft.AspNetCore.Builder.IEndpointConventionBuilder";
	const string AllowAnonymousExtensionsMetadataName = "Microsoft.AspNetCore.Builder.AuthorizationEndpointConventionBuilderExtensions";
	const string WithMetadataExtensionsMetadataName = "Microsoft.AspNetCore.Builder.RoutingEndpointConventionBuilderExtensions";

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

			start.RegisterSymbolAction(symbol => InspectAttributes(symbol.Symbol.GetAttributes(), symbol.Symbol.Name, marker, symbol.ReportDiagnostic),
				SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Property);

			// Minimal-API handlers are lambdas, not named members -- GetDeclaredSymbol never reaches an
			// anonymous function, so RegisterSymbolAction above is structurally blind to
			// `app.MapGet("/", [AllowAnonymous] () => ...)`. IAnonymousFunctionOperation.Symbol carries
			// the same attribute data a named method would; this is the only way to reach it.
			start.RegisterOperationAction(operation =>
			{
				var lambda = (IAnonymousFunctionOperation)operation.Operation;
				InspectAttributes(lambda.Symbol.GetAttributes(), "the endpoint handler", marker, operation.ReportDiagnostic);
			}, OperationKind.AnonymousFunction);

			var conventionBuilder = start.Compilation.GetTypeByMetadataName(BuilderMetadataName);
			if (conventionBuilder is null)
				return;

			// Identified by declaring type, not by namespace -- a namespace string is not the framework's
			// identity, and a third party is free to place its own same-named, same-shaped extension in
			// Microsoft.AspNetCore.Builder so its consumers need no extra `using`.
			var allowAnonymousExtensions = start.Compilation.GetTypeByMetadataName(AllowAnonymousExtensionsMetadataName);
			var withMetadataExtensions = start.Compilation.GetTypeByMetadataName(WithMetadataExtensionsMetadataName);

			start.RegisterOperationAction(operation =>
				InspectInvocation(operation, conventionBuilder, marker, allowAnonymousExtensions, withMetadataExtensions),
				OperationKind.Invocation);
		});
	}

	static void InspectAttributes(ImmutableArray<AttributeData> attributes, string culpritName, INamedTypeSymbol marker,
		Action<Diagnostic> reportDiagnostic)
	{
		foreach (var data in attributes)
		{
			// "Implements the marker", not "is AllowAnonymousAttribute" -- the law is about the metadata an
			// attribute contributes, and a custom attribute implementing IAllowAnonymous contributes exactly
			// the same thing.
			if (data.AttributeClass is not { } applied
				|| !applied.AllInterfaces.Contains(marker, SymbolEqualityComparer.Default))
				continue;
			if (data.ApplicationSyntaxReference?.GetSyntax() is not { } syntax)
				continue;

			reportDiagnostic(Diagnostic.Create(Diagnostics.AllowAnonymousBanned, syntax.GetLocation(), culpritName));
		}
	}

	static void InspectInvocation(OperationAnalysisContext context, INamedTypeSymbol conventionBuilder, INamedTypeSymbol marker,
		INamedTypeSymbol? allowAnonymousExtensions, INamedTypeSymbol? withMetadataExtensions)
	{
		var invocation = (IInvocationOperation)context.Operation;
		var method = invocation.TargetMethod;

		if (!method.IsExtensionMethod)
			return;

		// The receiver must be a convention builder. A user extension on string, or on their own type, is
		// none of this rule's business no matter what it is called.
		var receiver = method.ReducedFrom?.Parameters.FirstOrDefault()?.Type ?? method.Parameters
			.FirstOrDefault()?.Type;
		if (receiver is null || !SatisfiesConventionBuilder(receiver, conventionBuilder))
			return;

		if (method.Name == "AllowAnonymous"
			&& allowAnonymousExtensions is not null
			&& SymbolEqualityComparer.Default.Equals(method.ContainingType, allowAnonymousExtensions))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				Diagnostics.AllowAnonymousBanned, invocation.Syntax.GetLocation(), method.ContainingType.Name));
			return;
		}

		if (method.Name == "WithMetadata"
			&& withMetadataExtensions is not null
			&& SymbolEqualityComparer.Default.Equals(method.ContainingType, withMetadataExtensions))
			InspectMetadataArguments(context, invocation, marker);
	}

	// `.WithMetadata(new AllowAnonymousAttribute())` reaches the same IAllowAnonymous metadata the
	// attribute and .AllowAnonymous() shapes reach, through the framework's params-array escape hatch.
	static void InspectMetadataArguments(OperationAnalysisContext context, IInvocationOperation invocation, INamedTypeSymbol marker)
	{
		foreach (var argument in invocation.Arguments)
		{
			foreach (var item in ExpandMetadataItems(argument.Value))
			{
				var (type, operand) = UnwrapConversion(item);
				if (type is null || !type.AllInterfaces.Contains(marker, SymbolEqualityComparer.Default))
					continue;

				context.ReportDiagnostic(Diagnostic.Create(
					Diagnostics.AllowAnonymousBanned, operand.Syntax.GetLocation(), type.Name));
			}
		}
	}

	// `params object[] items` -- the compiler wraps inline arguments in an implicit array creation; unwrap
	// it to inspect each item instead of convicting the synthesized `object[]` itself.
	static ImmutableArray<IOperation> ExpandMetadataItems(IOperation value) =>
		value is IArrayCreationOperation { Initializer: { } initializer } ? initializer.ElementValues : [value];

	// Each boxed params element is wrapped in an implicit reference conversion to `object`; unwrap it to
	// see the real type the caller constructed.
	static (ITypeSymbol? Type, IOperation Operand) UnwrapConversion(IOperation operation) =>
		operation is IConversionOperation conversion ? UnwrapConversion(conversion.Operand) : (operation.Type, operation);

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
