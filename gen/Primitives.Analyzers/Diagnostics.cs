using Microsoft.CodeAnalysis;

namespace Norse.Primitives.Analyzers;

#pragma warning disable RS2008 // No analyzer-release ledger, matching the platform's other generators/analyzers.

/// <summary>
/// NORSE060 opens a new decade for Svartálfheim — the platform's existing per-block convention
/// (NORSE010 Asgard, NORSE011 Yggdrasil, NORSE020-021/NORSE022-028 Midgard, NORSE030-034 Urðarbrunnr,
/// NORSE040-049 reserved on paper for the well-seam-midgard-excision plan, NORSE050-051 Mímisbrunnr — and
/// NORSE070-079 now claimed for the architecture-law block (Architecture.Analyzers, 2026-08-03)).
/// A fresh platform-wide grep at authoring time confirmed NORSE052-NORSE059 clean.
/// </summary>
static class Diagnostics
{
	public static readonly DiagnosticDescriptor ResultInServiceResponse = new(
		"NORSE060", "Result<T> reachable in a [ServiceContract] response",
		"Member '{0}' on '{1}' is typed Result<T>, reachable from the response of '{2}.{3}' — Result<T> is deserialization-only and must never appear on a service response payload", "Norse.Primitives",
		DiagnosticSeverity.Error, isEnabledByDefault: true);
}
