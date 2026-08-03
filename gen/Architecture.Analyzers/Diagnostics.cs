using Microsoft.CodeAnalysis;

namespace Norse.Architecture.Analyzers;

#pragma warning disable RS2008 // No analyzer-release ledger, matching the platform's other generators/analyzers.

/// <summary>
/// NORSE070-079 — the architecture-law block, claimed 2026-08-03 (grep-confirmed clean at authoring;
/// the authoritative per-block ledger lives in Primitives.Analyzers' Diagnostics.cs header). All four
/// strikes are NotConfigurable errors: the law is not a severity preference, and no consuming realm
/// may downgrade it. Spec: ../Glitnir/docs/Platform/specs/2026-08-03-realm-dependency-law-compiler-enforcement-design.md.
/// </summary>
static class Diagnostics
{
	const string Category = "Norse.Architecture";

	public static readonly DiagnosticDescriptor WireFormatOutsideBorder = new(
		"NORSE070", "Wire format outside Midgard/Yggdrasil",
		"'{0}' is wire-format machinery — encodings exist in Infrastructure (Midgard) and Hosting (Yggdrasil) alone; declare intent with contract attributes and let the edge own the bytes", Category,
		DiagnosticSeverity.Error, isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.NotConfigurable);

	public static readonly DiagnosticDescriptor MidgardTakenAsDependency = new(
		"NORSE071", "Midgard taken as a dependency",
		"Assembly '{0}' references '{1}' — Infrastructure (Midgard) is consumed by Hosting (Yggdrasil) alone and publishes no surface; no realm takes Midgard as a dependency", Category,
		DiagnosticSeverity.Error, isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.NotConfigurable);

	public static readonly DiagnosticDescriptor CrossRealmReach = new(
		"NORSE072", "Cross-realm reach outside published surfaces",
		"Assembly '{0}' references '{1}' — {2}; realms are bounded contexts whose only doors are .Contracts, .Services, and .Components", Category,
		DiagnosticSeverity.Error, isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.NotConfigurable);

	public static readonly DiagnosticDescriptor ComponentImpurity = new(
		"NORSE073", "Component assembly impurity",
		"Component assembly '{0}' references '{1}' — .Components consumes foundation and published surfaces only, even within its own realm, so render mode stays a deployment detail", Category,
		DiagnosticSeverity.Error, isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.NotConfigurable);
}
