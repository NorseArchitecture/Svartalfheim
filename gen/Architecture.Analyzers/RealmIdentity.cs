using System.Collections.Immutable;

namespace Norse.Architecture.Analyzers;

/// <summary>
/// Jurisdiction from names alone (spec §3): the function vocabulary is law, the brand is everything
/// before the first recognized segment, realm families are inferred as the segment after the brand —
/// never enumerated, so onboarding a realm needs no analyzer release. Pure string functions;
/// no configuration exists, and none is honored.
/// </summary>
static class RealmIdentity
{
	public static readonly ImmutableHashSet<string> FunctionVocabulary =
		["Primitives", "Abstractions", "Persistence", "Messaging", "Infrastructure", "Hosting", "DesignSystem"];

	static readonly ImmutableArray<string> _exemptSuffixes =
		[".Tests", ".Benchmarks", ".Aot.Smoke", ".Analyzers", ".Generator", ".Generators"];

	static readonly ImmutableHashSet<string> _foundationFunctions =
		["Primitives", "Abstractions", "Persistence", "Messaging"];

	static readonly ImmutableArray<string> _publishedSurfaceSuffixes =
		[".Contracts", ".Services", ".Components"];

	public static bool IsExempt(string assemblyName) =>
		_exemptSuffixes.Any(s => assemblyName.EndsWith(s, StringComparison.Ordinal));

	public static string? FunctionOf(string assemblyName) =>
		assemblyName.Split('.').FirstOrDefault(FunctionVocabulary.Contains);

	public static bool IsWireBorder(string assemblyName) =>
		FunctionOf(assemblyName) is "Infrastructure" or "Hosting";

	public static string? BrandOf(string assemblyName)
	{
		var segments = assemblyName.Split('.');
		var index = Array.FindIndex(segments, FunctionVocabulary.Contains);
		return index > 0 ?
			string.Join(".", segments.Take(index)) :
			null;
	}

	public static string? FamilyOf(string assemblyName, string brand)
	{
		var prefix = $"{brand}.";
		return assemblyName.StartsWith(prefix, StringComparison.Ordinal) ?
			assemblyName.Substring(prefix.Length).Split('.')[0] :
			null;
	}

	public static bool IsPublishedSurface(string assemblyName) =>
		_publishedSurfaceSuffixes.Any(s => assemblyName.EndsWith(s, StringComparison.Ordinal)) ||
		assemblyName.Contains(".Components.", StringComparison.Ordinal);

	public static bool IsFoundation(string assemblyName, string brand) =>
		FamilyOf(assemblyName, brand) is { } family &&
		(_foundationFunctions.Contains(family) || assemblyName == $"{brand}.DesignSystem.Tokens");
}
