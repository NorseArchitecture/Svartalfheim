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

	// Codex review: frame tracking used to dispatch on invocation NAME alone, so an unrelated user
	// method sharing a RenderTreeBuilder method name could corrupt the frame stack. Here
	// EditContextFor's own argument calls a user type's CloseComponent() -- before the fix that name
	// alone popped the EditForm frame early, so the later real __builder.CloseComponent() found an
	// empty stack and NORSE075 never fired.
	[Fact]
	async Task An_unrelated_method_sharing_a_RenderTreeBuilder_name_does_not_corrupt_the_frame_stack()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			using Microsoft.AspNetCore.Components.Forms;
			using Microsoft.AspNetCore.Components.Rendering;
			namespace fixture;
			static class Sneaky
			{
				public static EditContext CloseComponent() => new(new object());
			}
			class ViolatingForm : Fixture.StubForm
			{
				readonly object _request = new();
				void Handle(EditContext context) { }
				protected override void BuildRenderTree(RenderTreeBuilder __builder)
				{
					__builder.OpenComponent<EditForm>(0);
					__builder.AddComponentParameter(1, "EditContext", EditContextFor(Sneaky.CloseComponent()));
					__builder.AddComponentParameter(2, "OnValidSubmit", default(EventCallback<EditContext>));
					__builder.CloseComponent();
				}
			}
			""";
		var diagnostics = await Analyze(source);
		diagnostics.ShouldContain(d => d.Id == "NORSE075");
	}

	// Codex review: the EditContextFor check used to be a textual suffix match, so a differently named
	// helper like CreateEditContextFor was mistaken for the seam and falsely convicted an otherwise
	// permitted OnValidSubmit.
	[Fact]
	async Task A_differently_named_helper_ending_in_EditContextFor_is_not_seam_bound()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			using Microsoft.AspNetCore.Components.Forms;
			using Microsoft.AspNetCore.Components.Rendering;
			namespace fixture;
			class NotSeamBoundForm : ComponentBase
			{
				readonly object _request = new();
				void Handle(EditContext context) { }
				EditContext CreateEditContextFor(object request) => new(request);
				protected override void BuildRenderTree(RenderTreeBuilder __builder)
				{
					__builder.OpenComponent<EditForm>(0);
					__builder.AddComponentParameter(1, "EditContext", CreateEditContextFor(_request));
					__builder.AddComponentParameter(2, "OnValidSubmit", default(EventCallback<EditContext>));
					__builder.CloseComponent();
				}
			}
			""";
		var diagnostics = await Analyze(source);
		diagnostics.ShouldBeEmpty();
	}
}
