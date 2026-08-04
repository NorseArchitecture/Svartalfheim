using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Norse.Primitives.Analyzers.Tests;

/// <summary>
/// Compilation-harness idiom mirroring Midgard's <c>GeneratorTestHarness</c>, adapted for a
/// <see cref="DiagnosticAnalyzer"/> rather than an <c>IIncrementalGenerator</c> — analyzer diagnostics
/// come from <see cref="CompilationWithAnalyzers.GetAnalyzerDiagnosticsAsync()"/>, not a generator driver.
/// <c>Outcome&lt;T&gt;</c> lives in Asgard; Svartálfheim rides beneath Asgard and must never reference it
/// (the reverse is the platform's real dependency direction — Asgard NorseRefs Primitives, confirmed via
/// its own <c>.csproj</c> files), so every fixture gets a same-shaped, same-metadata-name stub prepended,
/// mirroring how the Xml generator's own test harness stubs <c>GrpcControllerBase</c> before Asgard
/// shipped the real one.
/// </summary>
static class AnalyzerTestHarness
{
	public const string OutcomeStub = """
		namespace Norse.Abstractions.Contracts;

		public readonly struct Outcome<T>
		{
		}
		""";

	public static readonly MetadataReference[] ExtraReferences =
	[
		MetadataReference.CreateFromFile(typeof(Result<>).Assembly.Location),
		.. ReferenceAssemblies.Bcl
	];

	/// <summary>Preview lang version — <c>Result&lt;T&gt;</c>/<c>Success&lt;T&gt;</c> carry the <c>[Union]</c> C# 15 preview attribute, and fixtures freely construct/pattern-match them.</summary>
	public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

	/// <summary>Builds the fixture compilation, <see cref="OutcomeStub"/> included, unrun.</summary>
	public static CSharpCompilation CreateCompilation(params string[] sources) =>
		CSharpCompilation.Create(
			"Norse.Primitives.Analyzers.Fixtures",
			[.. new[] { OutcomeStub }.Concat(sources).Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions))],
			ExtraReferences,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

	/// <summary>Compiles the fixture (asserting it compiles clean — a fixture typo must fail loudly here, never masquerade as "zero diagnostics") and runs NORSE060's analyzer against it.</summary>
	public static Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(params string[] sources) =>
		GetDiagnosticsAsync(new ResultInServiceResponseAnalyzer(), sources);

	/// <summary>Compiles the fixture (asserting it compiles clean — a fixture typo must fail loudly here, never masquerade as "zero diagnostics") and runs <paramref name="analyzer"/> against it.</summary>
	public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(DiagnosticAnalyzer analyzer, params string[] sources)
	{
		var compilation = CreateCompilation(sources);
		var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
		compileErrors.ShouldBeEmpty($"Fixture failed to compile:\n{string.Join("\n", compileErrors)}");

		var withAnalyzers = compilation.WithAnalyzers([analyzer]);
		return await withAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
	}
}
