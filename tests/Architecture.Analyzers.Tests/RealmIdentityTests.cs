namespace Norse.Architecture.Analyzers.Tests;

public sealed class RealmIdentityTests
{
	[Theory]
	[InlineData("Norse.Primitives.Tests")]
	[InlineData("Norse.Identity.Web.Server.Tests")]
	[InlineData("Norse.Primitives.Benchmarks")]
	[InlineData("Norse.Primitives.Aot.Smoke")]
	[InlineData("Norse.Primitives.Analyzers")]
	[InlineData("Norse.Architecture.Analyzers")]
	[InlineData("Norse.Abstractions.Web.Server.Generator")]
	void Exempts_evidence_rigs_and_build_tooling(string name) =>
		RealmIdentity.IsExempt(name).ShouldBeTrue();

	[Theory]
	[InlineData("Norse.Primitives", "Primitives")]
	[InlineData("Norse.Infrastructure.Web.Server", "Infrastructure")]
	[InlineData("Acme.Corp.Primitives", "Primitives")]
	[InlineData("Norse.Identity.Web.Server", null)]
	void Finds_the_function_segment_by_vocabulary(string name, string? expected) =>
		RealmIdentity.FunctionOf(name).ShouldBe(expected);

	[Theory]
	[InlineData("Norse.Infrastructure.Web.Server", true)]
	[InlineData("Norse.Hosting.Web.Client", true)]
	[InlineData("Norse.Primitives", false)]
	[InlineData("Norse.Identity.Web.Server", false)]
	void Wire_border_is_infrastructure_or_hosting(string name, bool expected) =>
		RealmIdentity.IsWireBorder(name).ShouldBe(expected);

	[Theory]
	[InlineData("Norse.Primitives", "Norse")]
	[InlineData("Acme.Corp.Persistence.EntityFramework", "Acme.Corp")]
	[InlineData("Norse.Identity.Web.Server", null)]
	void Brand_is_everything_before_the_first_vocabulary_segment(string name, string? expected) =>
		RealmIdentity.BrandOf(name).ShouldBe(expected);

	[Theory]
	[InlineData("Norse.Identity.Web.Server", "Norse", "Identity")]
	[InlineData("Norse.Reference.Data.Entities", "Norse", "Reference")]
	[InlineData("Norse.Primitives.Ingestion", "Norse", "Primitives")]
	[InlineData("Acme.Identity", "Norse", null)]
	void Family_is_the_segment_after_the_brand(string name, string brand, string? expected) =>
		RealmIdentity.FamilyOf(name, brand).ShouldBe(expected);

	[Theory]
	[InlineData("Norse.AuthN.Contracts", true)]
	[InlineData("Norse.AuthN.Services", true)]
	[InlineData("Norse.AuthN.Components", true)]
	[InlineData("Norse.AuthN.Components.FluentUI", false)]
	[InlineData("Norse.Identity.EntityFramework", false)]
	void Published_surfaces_are_contracts_services_components(string name, bool expected) =>
		RealmIdentity.IsPublishedSurface(name).ShouldBe(expected);

	[Theory]
	[InlineData("Norse.Primitives", true)]
	[InlineData("Norse.Abstractions.Keys", true)]
	[InlineData("Norse.Persistence.EntityFramework.PostgreSQL", true)]
	[InlineData("Norse.Messaging.NServiceBus", true)]
	[InlineData("Norse.DesignSystem.Tokens", true)]
	[InlineData("Norse.DesignSystem.Stories", false)]
	[InlineData("Norse.Infrastructure.Keys", false)]
	[InlineData("Norse.Identity.EntityFramework", false)]
	void Foundation_is_the_four_families_plus_the_token_seed(string name, bool expected) =>
		RealmIdentity.IsFoundation(name, "Norse").ShouldBe(expected);
}
