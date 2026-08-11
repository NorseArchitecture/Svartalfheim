using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Norse.Architecture.Analyzers.Tests;

public sealed class ForcedLoadAnalyzerTests
{
	const string ComponentSource = """
		using Microsoft.AspNetCore.Components;
		namespace Norse.Hosting.Web.Components;
		public class RedirectToLogin
		{
			public void Go(NavigationManager navigation) =>
				navigation.NavigateTo("/", forceLoad: true);
		}
		""";

	static MetadataReference NavigationStub() =>
		CSharpCompilation.Create("Microsoft.AspNetCore.Components",
			[CSharpSyntaxTree.ParseText("""
				namespace Microsoft.AspNetCore.Components;
				public readonly struct NavigationOptions
				{
					public bool ForceLoad { get; init; }
					public bool ReplaceHistoryEntry { get; init; }
				}
				public abstract class NavigationManager
				{
					public void NavigateTo(string uri, bool forceLoad = false, bool replace = false) { }
					public void NavigateTo(string uri, NavigationOptions options) { }
				}
				""", AnalyzerTestHarness.ParseOptions)],
			ReferenceAssemblies.Bcl,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
			.ToMetadataReference();

	[Fact]
	async Task A_component_assembly_forcing_a_load_is_convicted()
	{
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Hosting.Web.Components", [NavigationStub()], ComponentSource);
		diagnostics.ShouldContain(d => d.Id == "NORSE074");
	}

	[Fact]
	async Task The_production_implementation_itself_is_absolved()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.AuthN.Components;
			sealed class ForceLoadSessionTransition(NavigationManager navigation)
			{
				public void Begin(string nextUrl) =>
					navigation.NavigateTo(nextUrl, forceLoad: true);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.AuthN.Components", [NavigationStub()], source);
		diagnostics.ShouldBeEmpty();
	}

	// The rejected opt-out, proven closed at both widths: the gate ASSEMBLY is not exempt (its own
	// pages live there), and the implementation's TYPE NAME minted in another assembly is not exempt.
	[Fact]
	async Task Another_type_inside_the_gate_assembly_is_convicted()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.AuthN.Components;
			public class Logout
			{
				public void Go(NavigationManager navigation) =>
					navigation.NavigateTo("/", forceLoad: true);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.AuthN.Components", [NavigationStub()], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE074");
	}

	[Fact]
	async Task The_implementation_type_name_minted_elsewhere_is_convicted()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.AuthN.Components;
			sealed class ForceLoadSessionTransition(NavigationManager navigation)
			{
				public void Begin(string nextUrl) =>
					navigation.NavigateTo(nextUrl, forceLoad: true);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Sneaky.Components", [NavigationStub()], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE074");
	}

	// Fail-loud: anything not provably soft convicts — the evasions the constant-only draft missed.
	[Fact]
	async Task A_variable_forceLoad_argument_is_convicted()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.Hosting.Web.Components;
			public class Page
			{
				public void Go(NavigationManager navigation)
				{
					bool forced = true;
					navigation.NavigateTo("/", forced);
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Hosting.Web.Components", [NavigationStub()], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE074");
	}

	[Fact]
	async Task A_prebuilt_options_value_is_convicted()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.Hosting.Web.Components;
			public class Page
			{
				public void Go(NavigationManager navigation)
				{
					NavigationOptions options = new() { ForceLoad = true };
					navigation.NavigateTo("/", options);
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Hosting.Web.Components", [NavigationStub()], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE074");
	}

	[Fact]
	async Task An_inline_options_without_ForceLoad_is_clean()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.Hosting.Web.Components;
			public class Page
			{
				public void Go(NavigationManager navigation) =>
					navigation.NavigateTo("/", new NavigationOptions { ReplaceHistoryEntry = true });
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Hosting.Web.Components", [NavigationStub()], source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task An_explicit_constant_false_is_clean()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.Hosting.Web.Components;
			public class Page
			{
				public void Go(NavigationManager navigation) =>
					navigation.NavigateTo("/", forceLoad: false);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Hosting.Web.Components", [NavigationStub()], source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task A_positional_true_is_convicted()
	{
		var source = ComponentSource.Replace("forceLoad: true", "true", StringComparison.Ordinal);
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Reference.Components", [NavigationStub()], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE074");
	}

	[Fact]
	async Task NavigationOptions_with_ForceLoad_true_is_convicted()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.Hosting.Web.Components;
			public class Page
			{
				public void Go(NavigationManager navigation) =>
					navigation.NavigateTo("/", new NavigationOptions { ForceLoad = true });
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Hosting.Web.Components", [NavigationStub()], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE074");
	}

	[Fact]
	async Task A_soft_navigation_is_clean()
	{
		var source = """
			using Microsoft.AspNetCore.Components;
			namespace Norse.Hosting.Web.Components;
			public class Page
			{
				public void Go(NavigationManager navigation) =>
					navigation.NavigateTo("/Account/Login");
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Hosting.Web.Components", [NavigationStub()], source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task A_test_assembly_is_exempt()
	{
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new ForcedLoadAnalyzer(), "Norse.Hosting.Web.Components.Tests", [NavigationStub()], ComponentSource);
		diagnostics.ShouldBeEmpty();
	}
}
