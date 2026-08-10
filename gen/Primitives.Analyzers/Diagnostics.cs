using Microsoft.CodeAnalysis;

namespace Norse.Primitives.Analyzers;

#pragma warning disable RS2008 // No analyzer-release ledger, matching the platform's other generators/analyzers.

/// <summary>
/// NORSE060 opened this decade for Svartálfheim; NORSE061/NORSE062 extend it (NORSE063 reserved for
/// a future generic decrypted-PII query surface, per the 2026-08-03 PII spec §4.1). The platform's
/// per-block convention: NORSE010 Asgard, NORSE011 Yggdrasil, NORSE020-021/NORSE022-029/NORSE035-037 Midgard,
/// NORSE030-034 Urðarbrunnr, NORSE040-049 reserved on paper for the well-seam-midgard-excision plan,
/// NORSE050-051 Mímisbrunnr, NORSE060-069 Svartálfheim; NORSE070-079 claimed for the architecture-law
/// block (<c>Architecture.Analyzers</c>, 2026-08-03). A fresh platform-wide grep at authoring time
/// confirmed NORSE061-NORSE069 clean.
/// </summary>
static class Diagnostics
{
	public static readonly DiagnosticDescriptor ResultInServiceResponse = new(
		"NORSE060", "Result<T> reachable in a [ServiceContract] response",
		"Member '{0}' on '{1}' is typed Result<T>, reachable from the response of '{2}.{3}' — Result<T> is deserialization-only and must never appear on a service response payload", "Norse.Primitives",
		DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor PiiWithoutRetentionPolicy = new(
		"NORSE061", "PII property has no [RetentionPolicy] declaration",
		"PII property '{0}' on persisted entity '{1}' has no [RetentionPolicy] declaration — every persisted PII field names its retention basis at compile time", "Norse.Primitives",
		DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor PiiNotDirectScalar = new(
		"NORSE062", "PII must be a direct scalar property of the persisted entity",
		"PII type '{0}' is reachable through member '{1}' on persisted entity '{2}' but is not a direct scalar property — PII persists only in direct scalar columns where the encrypting converter reaches; project the masked string instead", "Norse.Primitives",
		DiagnosticSeverity.Error, isEnabledByDefault: true);
}
