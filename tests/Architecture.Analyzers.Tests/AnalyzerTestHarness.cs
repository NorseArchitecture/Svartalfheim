using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Norse.Architecture.Analyzers.Tests;

static class AnalyzerTestHarness
{
	public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

	public static CSharpCompilation CreateCompilation(string assemblyName, MetadataReference[] extraReferences, params string[] sources) =>
		CreateCompilation(assemblyName, extraReferences, ParseOptions, sources);

	public static CSharpCompilation CreateCompilation(string assemblyName, MetadataReference[] extraReferences, CSharpParseOptions parseOptions, params string[] sources) =>
		CSharpCompilation.Create(
			assemblyName,
			[.. sources.Select(s => CSharpSyntaxTree.ParseText(s, parseOptions))],
			[.. ReferenceAssemblies.Bcl, .. extraReferences],
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

	public static MetadataReference CreateNorseReference(string assemblyName) =>
		CSharpCompilation.Create(assemblyName, [], ReferenceAssemblies.Bcl, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
			.ToMetadataReference();

	public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
		DiagnosticAnalyzer analyzer, string assemblyName, MetadataReference[] extraReferences, params string[] sources) =>
		await GetDiagnosticsAsync([analyzer], assemblyName, ParseOptions, extraReferences, sources);

	public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
		DiagnosticAnalyzer analyzer, string assemblyName, CSharpParseOptions parseOptions, MetadataReference[] extraReferences, params string[] sources) =>
		await GetDiagnosticsAsync([analyzer], assemblyName, parseOptions, extraReferences, sources);

	public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
		DiagnosticAnalyzer[] analyzers, string assemblyName, MetadataReference[] extraReferences, params string[] sources) =>
		await GetDiagnosticsAsync(analyzers, assemblyName, ParseOptions, extraReferences, sources);

	public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
		DiagnosticAnalyzer[] analyzers, string assemblyName, CSharpParseOptions parseOptions, MetadataReference[] extraReferences, params string[] sources)
	{
		var compilation = CreateCompilation(assemblyName, extraReferences, parseOptions, sources);
		var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
		compileErrors.ShouldBeEmpty($"Fixture failed to compile:\n{string.Join("\n", compileErrors)}");

		// reportSuppressedDiagnostics: true — a diagnostic silenced by a #pragma or a [SuppressMessage]
		// must stay visible to the fixtures proving NORSE079 (the meta-strike) and the NotConfigurable
		// pragma-survival test; both assert on IsSuppressed rather than on absence from the collection.
		var withAnalyzers = compilation.WithAnalyzers(
			[.. analyzers],
			new CompilationWithAnalyzersOptions(
				options: new AnalyzerOptions([]), onAnalyzerException: (Action<Exception, DiagnosticAnalyzer, Diagnostic>?)null,
				concurrentAnalysis: true, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true));
		return await withAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
	}
}
