using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Norse.Architecture.Analyzers.Tests;

public sealed class RealmReferenceAnalyzerTests
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
	async Task Strikes_norse071_when_a_realm_references_midgard()
	{
		var diagnostics = await RunAsync("Norse.Identity.Web.Server", "Norse.Infrastructure.Web.Server");
		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE071");
		diagnostic.Location.ShouldBe(Location.None);
		diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("Norse.Identity.Web.Server");
		diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("Norse.Infrastructure.Web.Server");
	}

	[Fact]
	async Task Precedence_infrastructure_contracts_is_not_a_door()
	{
		// Spec §2 precedence ruling: NORSE071 evaluates before the published-surface arm and wins.
		(await RunAsync("Norse.Identity.Web.Server", "Norse.Infrastructure.Contracts"))
			.ShouldContain(d => d.Id == "NORSE071");
	}

	[Fact]
	async Task The_tree_may_reference_midgard_and_midgard_may_reference_itself()
	{
		(await RunAsync("Norse.Hosting.Web.Server", "Norse.Infrastructure.Web.Server")).ShouldBeEmpty();
		(await RunAsync("Norse.Infrastructure.Web.Server", "Norse.Infrastructure.Web.Client")).ShouldBeEmpty();
	}

	[Fact]
	async Task Realms_inherit_the_foundation_freely()
	{
		(await RunAsync("Norse.Identity.EntityFramework",
			"Norse.Primitives", "Norse.Abstractions.Contracts", "Norse.Persistence.EntityFramework", "Norse.Messaging.NServiceBus", "Norse.DesignSystem.Tokens"))
			.ShouldBeEmpty();
	}

	[Fact]
	async Task Own_realm_and_published_surfaces_are_legal_doors()
	{
		(await RunAsync("Norse.Identity.Web.Server",
			"Norse.Primitives", "Norse.Identity.EntityFramework", "Norse.AuthN.Services", "Norse.AuthN.Contracts", "Norse.Reference.Components"))
			.ShouldBeEmpty();
	}

	[Fact]
	async Task Strikes_norse072_when_a_realm_reaches_into_a_foreign_realm()
	{
		var diagnostics = await RunAsync("Norse.Reference.Data.Entities", "Norse.Primitives", "Norse.Identity.EntityFramework");
		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE072");
		diagnostic.Location.ShouldBe(Location.None);
		diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("not foundation");
	}

	[Fact]
	async Task The_forge_references_no_foreign_norse_assembly()
	{
		(await RunAsync("Norse.Primitives.Ingestion", "Norse.Primitives")).ShouldBeEmpty();
		(await RunAsync("Norse.Primitives", "Norse.Abstractions.Contracts"))
			.ShouldContain(d => d.Id == "NORSE072");
	}

	[Fact]
	async Task Asgard_references_only_the_forge()
	{
		(await RunAsync("Norse.Abstractions.Keys", "Norse.Primitives", "Norse.Abstractions.Contracts")).ShouldBeEmpty();
		(await RunAsync("Norse.Abstractions.Keys", "Norse.Persistence.EntityFramework"))
			.ShouldContain(d => d.Id == "NORSE072");
	}

	[Fact]
	async Task Brand_is_inferred_from_references_when_the_name_has_no_anchor()
	{
		// Norse.Identity.Web.Server carries no vocabulary segment; the Norse.Abstractions.Contracts
		// reference anchors the brand, making Norse.Identity.EntityFramework same-family-legal and
		// Norse.Reference.Data.Entities a conviction.
		(await RunAsync("Norse.Identity.Web.Server", "Norse.Abstractions.Contracts", "Norse.Identity.EntityFramework"))
			.ShouldBeEmpty();
		(await RunAsync("Norse.Identity.Web.Server", "Norse.Abstractions.Contracts", "Norse.Reference.Data.Entities"))
			.ShouldContain(d => d.Id == "NORSE072");
	}

	[Fact]
	async Task Cross_brand_references_are_ungoverned()
	{
		(await RunAsync("Norse.Identity.Web.Server", "Norse.Abstractions.Contracts", "Acme.Identity.EntityFramework"))
			.ShouldBeEmpty();
	}

	[Fact]
	async Task Non_norse_assemblies_are_ignored_entirely()
	{
		(await RunAsync("Norse.Identity.Web.Server", "Norse.Abstractions.Contracts", "FluentValidation", "Npgsql"))
			.ShouldBeEmpty();
	}
}
