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
		// formula (own realm legal), not the purity stricture. Since the 2026-08-03 vendor-drop door
		// ruling, a Components vendor drop is also a published-surface TARGET in its own right (the
		// ".Components." segment widening on RealmIdentity.IsPublishedSurface) — so it's reachable from
		// any realm under the general formula, not only via its own realm or the foundation tree.
		(await RunAsync("Norse.AuthN.Components.FluentUI", "Norse.Abstractions.Contracts", "Norse.AuthN.Components"))
			.ShouldBeEmpty();
	}

	[Fact]
	async Task A_server_realm_may_reference_a_sibling_components_vendor_drop()
	{
		// The exact Himinbjörg/Bragi shape from master: a server assembly (or an anchor referencing it)
		// takes a Components vendor drop as a dependency. Legal under the general formula's
		// published-surface arm now that ".Components." widening makes the drop a door.
		(await RunAsync("Norse.Identity.Web.Server", "Norse.Primitives", "Norse.AuthN.Components.FluentUI"))
			.ShouldBeEmpty();
	}
}
