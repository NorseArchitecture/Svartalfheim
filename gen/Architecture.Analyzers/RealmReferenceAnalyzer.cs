using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Norse.Architecture.Analyzers;

/// <summary>
/// NORSE071/NORSE072/NORSE073 (spec §2): the reference formula with its precedence ruling. Evaluated
/// over Compilation.ReferencedAssemblyNames — transitively-flowing compile assets included, which is
/// correct law (a transitive dependency is still a dependency); reports land at Location.None naming
/// source, target, and the failed arms, so a transitive strike costs a glance, not an archaeology dig.
/// Brand is the compilation's own anchor when its name carries a vocabulary segment, otherwise
/// inferred from the first referenced assembly whose anchor-derived brand prefixes the compilation's
/// name. Cross-brand and non-Norse references are ungoverned — deliberate and recorded.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RealmReferenceAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[Diagnostics.MidgardTakenAsDependency, Diagnostics.CrossRealmReach, Diagnostics.ComponentImpurity];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationAction(static compilationContext =>
		{
			var compilation = compilationContext.Compilation;
			var self = compilation.AssemblyName ?? "";
			if (self.Length == 0 || RealmIdentity.IsExempt(self))
				return;

			ImmutableArray<string> references = [.. compilation.ReferencedAssemblyNames.Select(r => r.Name)];
			var brand = ResolveBrand(self, references);
			if (brand is null)
				return; // No anchor anywhere: nothing is governed (NORSE070 covers Law #1 regardless).

			var selfFunction = RealmIdentity.FunctionOf(self);
			var selfFamily = RealmIdentity.FamilyOf(self, brand);
			var isComponents = self.EndsWith(".Components", StringComparison.Ordinal);

			foreach (var target in references)
			{
				if (RealmIdentity.FamilyOf(target, brand) is not { } targetFamily)
					continue; // cross-brand / non-Norse: ungoverned.

				// Precedence 1 (spec §2 ruling): Midgard as a target beats every arm, doors included.
				if (RealmIdentity.FunctionOf(target) == "Infrastructure" && selfFunction is not ("Infrastructure" or "Hosting"))
				{
					compilationContext.ReportDiagnostic(Diagnostic.Create(
						Diagnostics.MidgardTakenAsDependency, Location.None, self, target));
					continue;
				}

				var sameFamily = targetFamily == selfFamily;

				// Precedence 2: foundation internal ordering replaces the general formula.
				if (selfFamily == "Primitives" && !sameFamily)
				{
					ReportReach(compilationContext, self, target, "the forge references no Norse assembly outside its own family");
					continue;
				}
				if (selfFamily == "Abstractions" && !sameFamily && targetFamily != "Primitives")
				{
					ReportReach(compilationContext, self, target, "Asgard references only Svartalfheim");
					continue;
				}

				// Task 5 fills the .Components stricture here (NORSE073).

				if (RealmIdentity.IsFoundation(target, brand) || sameFamily ||
					RealmIdentity.IsPublishedSurface(target) || selfFunction == "Hosting")
					continue;

				ReportReach(compilationContext, self, target,
					"not foundation, a different realm, and not a published surface");
			}
		});
	}

	static string? ResolveBrand(string self, ImmutableArray<string> references) =>
		RealmIdentity.BrandOf(self) ??
		references
			.Select(RealmIdentity.BrandOf)
			.FirstOrDefault(b => b is not null && self.StartsWith($"{b}.", StringComparison.Ordinal));

	static void ReportReach(CompilationAnalysisContext context, string self, string target, string failedArms) =>
		context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CrossRealmReach, Location.None, self, target, failedArms));
}
