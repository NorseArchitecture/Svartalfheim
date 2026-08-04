using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Norse.Primitives.Analyzers;

/// <summary>
/// NORSE061/NORSE062 — the compile-time retention gate (2026-08-03 PII spec §5). Roots are types
/// implementing <c>INorseEntity&lt;TSelf&gt;</c>. A direct property whose (nullable-unwrapped) type
/// implements <c>IMaskedValue</c> must carry <c>[RetentionPolicy]</c> (NORSE061). An
/// <c>IMaskedValue</c> implementer reachable any other way — nested composition, collection element,
/// array — is banned outright (NORSE062): the encrypting value converter operates per scalar column,
/// so nested PII would serialize into JSON documents as plaintext, a shredder escape no attribute cures.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RetentionPolicyAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Diagnostics.PiiWithoutRetentionPolicy, Diagnostics.PiiNotDirectScalar];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(OnCompilationStart);
	}

	static void OnCompilationStart(CompilationStartAnalysisContext context)
	{
		if (RetentionWellKnownTypes.Resolve(context.Compilation) is not RetentionWellKnownTypes wellKnown)
			return;

		context.RegisterSymbolAction(c => AnalyzeType(c, wellKnown), SymbolKind.NamedType);
	}

	static void AnalyzeType(SymbolAnalysisContext context, RetentionWellKnownTypes wellKnown)
	{
		var type = (INamedTypeSymbol)context.Symbol;
		if (!IsNorseEntityRoot(type, wellKnown.NorseEntity))
			return;

		// Walk the base chain so an inherited PII property (a shared base class like
		// NorseEntityBase<TSelf>, or a brownfield Identity base) doesn't bypass the gate — but stop
		// before walking through another root's own base chain: that root is analyzed separately
		// when SymbolAction visits it, so this root analyzes only up to, not through, the next one.
		for (var t = type; t is not null; t = t.BaseType)
		{
			if (!SymbolEqualityComparer.Default.Equals(t, type) && IsNorseEntityRoot(t, wellKnown.NorseEntity))
				break;

			foreach (var property in t.GetMembers().OfType<IPropertySymbol>())
			{
				if (property is not { IsStatic: false, DeclaredAccessibility: Accessibility.Public })
					continue;
				AnalyzeProperty(context, wellKnown, type, property);
			}
		}
	}

	static void AnalyzeProperty(SymbolAnalysisContext context, RetentionWellKnownTypes wellKnown, INamedTypeSymbol type, IPropertySymbol property)
	{
		// Explicit three-way, in law order. (1) Nullable<T> unwraps to a DIRECT scalar — the
		// only wrapper that stays scalar; arrays route through IArrayTypeSymbol and collections
		// through IEnumerable<T>, so neither can sneak into this branch.
		var scalarType = property.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ?
			nullable.TypeArguments[0] :
			property.Type;
		if (PiiCompositionWalker.Implements(scalarType, wellKnown.MaskedValue))
		{
			if (!HasRetentionPolicy(property, wellKnown.RetentionPolicyAttribute))
				Report(context, Diagnostics.PiiWithoutRetentionPolicy, property, property.Name, type.Name);
			return;
		}

		// (2) Array/collection whose element (transitively unwrapped) is PII — banned, no cure.
		var element = PiiCompositionWalker.Unwrap(scalarType);
		if (!SymbolEqualityComparer.Default.Equals(element, scalarType) &&
			PiiCompositionWalker.Implements(element, wellKnown.MaskedValue))
		{
			Report(context, Diagnostics.PiiNotDirectScalar, property, element.Name, property.Name, type.Name);
			return;
		}

		// (3) PII hiding anywhere inside the composed type's closure — banned, no cure.
		if (PiiCompositionWalker.FindReachablePii(element, wellKnown.MaskedValue) is { } nested)
			Report(context, Diagnostics.PiiNotDirectScalar, property, nested.Name, property.Name, type.Name);
	}

	static bool IsNorseEntityRoot(INamedTypeSymbol type, INamedTypeSymbol norseEntity) =>
		type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, norseEntity));

	static bool HasRetentionPolicy(IPropertySymbol property, INamedTypeSymbol retentionPolicy) =>
		property.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, retentionPolicy));

	static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor,
		IPropertySymbol property, params object[] args) =>
		context.ReportDiagnostic(Diagnostic.Create(descriptor, property.Locations[0], args));
}
