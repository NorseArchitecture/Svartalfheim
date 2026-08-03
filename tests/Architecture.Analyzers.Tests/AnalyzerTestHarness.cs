using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Norse.Architecture.Analyzers.Tests;

static class AnalyzerTestHarness
{
	public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

	public static CSharpCompilation CreateCompilation(string assemblyName, MetadataReference[] extraReferences, params string[] sources) =>
		CSharpCompilation.Create(
			assemblyName,
			[.. sources.Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions))],
			[.. ReferenceAssemblies.Bcl, .. extraReferences],
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

	public static MetadataReference CreateNorseReference(string assemblyName) =>
		CSharpCompilation.Create(assemblyName, [], ReferenceAssemblies.Bcl, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
			.ToMetadataReference();

	public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
		DiagnosticAnalyzer analyzer, string assemblyName, MetadataReference[] extraReferences, params string[] sources)
	{
		var compilation = CreateCompilation(assemblyName, extraReferences, sources);
		var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
		compileErrors.ShouldBeEmpty($"Fixture failed to compile:\n{string.Join("\n", compileErrors)}");

		var withAnalyzers = compilation.WithAnalyzers([analyzer]);
		return await withAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
	}
}
