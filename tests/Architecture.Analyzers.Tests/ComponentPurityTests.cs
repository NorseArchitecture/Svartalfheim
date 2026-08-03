using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Norse.Architecture.Analyzers.Tests;

public sealed class ComponentPurityTests
{
	const string Empty =
		"""
		namespace App;

		static class Anchor;
		""";

	static async Task<ImmutableArray<Diagnostic>> RunAsync(string self, params string[] references) =>
		await AnalyzerTestHarness.GetDiagnosticsAsync(
			new RealmReferenceAnalyzer(), self,
			[.. references.Select(AnalyzerTestHarness.CreateNorseReference)], Empty);

	[Fact]
	async Task Strikes_norse073_when_components_reference_their_own_realm_server_side()
	{
		var diagnostics = await RunAsync("Norse.AuthN.Components", "Norse.Abstractions.Contracts", "Norse.Identity.EntityFramework");
		diagnostics.ShouldContain(d => d.Id == "NORSE073");

		// Even the SAME realm's server assembly is out of reach — that is the whole point of Law #3.
		(await RunAsync("Norse.AuthN.Components", "Norse.Abstractions.Contracts", "Norse.AuthN.Web.Server"))
			.ShouldContain(d => d.Id == "NORSE073");
	}

	[Fact]
	async Task Components_ride_foundation_and_published_surfaces_freely()
	{
		(await RunAsync("Norse.AuthN.Components",
			"Norse.Primitives", "Norse.Abstractions.Contracts", "Norse.DesignSystem.Tokens", "Norse.AuthN.Services", "Norse.Reference.Components"))
			.ShouldBeEmpty();
	}

	[Fact]
	async Task Midgard_precedence_still_beats_the_component_stricture()
	{
		(await RunAsync("Norse.AuthN.Components", "Norse.Abstractions.Contracts", "Norse.Infrastructure.Web.Client"))
			.ShouldContain(d => d.Id == "NORSE071");
	}

	[Fact]
	async Task A_components_fluentui_drop_is_not_itself_a_components_assembly()
	{
		// Norse.AuthN.Components.FluentUI does not end ".Components" — it is governed by the general
		// formula (own realm legal), not the purity stricture. Deliberate: vendor drops may reference
		// their sibling base Components assembly and vendor packages freely.
		(await RunAsync("Norse.AuthN.Components.FluentUI", "Norse.Abstractions.Contracts", "Norse.AuthN.Components"))
			.ShouldBeEmpty();
	}
}
