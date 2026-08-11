using Microsoft.CodeAnalysis;

namespace Norse.Architecture.Analyzers.Tests;

public sealed class SeamBoundFormAnalyzerTests
{
	// The generated fixtures reference the real component assemblies — resolved from the test's own
	// runtime, the same way ReferenceAssemblies.Bcl resolves the BCL.
	static readonly MetadataReference[] _componentReferences =
	[
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Components.ComponentBase).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Components.Forms.EditContext).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Components.Forms.EditForm).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Components.Web.HeadContent).Assembly.Location),
	];

	static Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> Analyze(string generatedSource) =>
		AnalyzerTestHarness.GetDiagnosticsAsync(new SeamBoundFormAnalyzer(),
			"Norse.AuthN.Components.FluentUI", _componentReferences,
			RazorGeneratedFixtures.StubFormSource, generatedSource);

	[Fact]
	async Task The_generated_violating_form_is_convicted()
	{
		var diagnostics = await Analyze(RazorGeneratedFixtures.ViolatingForm);
		diagnostics.ShouldContain(d => d.Id == "NORSE075");
	}

	[Fact]
	async Task The_generated_clean_form_passes()
	{
		var diagnostics = await Analyze(RazorGeneratedFixtures.CleanForm);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task A_model_bound_scaffold_form_with_OnValidSubmit_is_not_convicted()
	{
		var diagnostics = await Analyze(RazorGeneratedFixtures.ModelBoundForm);
		diagnostics.ShouldBeEmpty();
	}
}
