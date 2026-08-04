using Microsoft.CodeAnalysis;

namespace Norse.Primitives.Analyzers.Tests;

public sealed class RetentionPolicyAnalyzerTests
{
	// Svartálfheim cannot reference Urðarbrunnr — stub INorseEntity with the identical metadata name,
	// following the harness's existing OutcomeStub pattern.
	const string EntityStub =
		"""
		namespace Norse.Persistence.EntityFramework
		{
			public interface INorseEntity<TSelf> where TSelf : class, INorseEntity<TSelf>
			{
			}
		}
		""";

	const string PiiFixture =
		"""
		using Norse.Primitives.Pii;
		namespace Fixtures
		{
			public readonly record struct TestEmail : IMaskedValue
			{
				public string Masked => "***";
				public string ToMasked(System.DateOnly asOf) => Masked;
			}
		}
		""";

	[Fact]
	async Task Reports_norse061_when_pii_property_has_no_retention_policy()
	{
		var source =
			"""
			using Fixtures;
			using Norse.Persistence.EntityFramework;
			namespace App
			{
				public sealed class Person : INorseEntity<Person>
				{
					public TestEmail Email { get; init; }
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetentionPolicyAnalyzer(), EntityStub, PiiFixture, source);
		diagnostics.ShouldContain(d => d.Id == "NORSE061");
	}

	[Fact]
	async Task Reports_nothing_when_pii_property_declares_retention_policy()
	{
		var source =
			"""
			using Fixtures;
			using Norse.Persistence.EntityFramework;
			using Norse.Primitives.Pii;
			namespace App
			{
				public sealed class Person : INorseEntity<Person>
				{
					[RetentionPolicy(RetentionBasis.SubjectKey)]
					public TestEmail Email { get; init; }
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetentionPolicyAnalyzer(), EntityStub, PiiFixture, source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Reports_norse061_when_nullable_pii_property_has_no_retention_policy()
	{
		var source =
			"""
			using Fixtures;
			using Norse.Persistence.EntityFramework;
			namespace App
			{
				public sealed class Person : INorseEntity<Person>
				{
					public TestEmail? Email { get; init; }
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetentionPolicyAnalyzer(), EntityStub, PiiFixture, source);
		diagnostics.ShouldContain(d => d.Id == "NORSE061");
	}

	[Fact]
	async Task Reports_norse062_when_pii_hides_inside_a_composed_type()
	{
		var source =
			"""
			using Fixtures;
			using Norse.Persistence.EntityFramework;
			using Norse.Primitives.Pii;
			namespace App
			{
				public sealed class ContactCard
				{
					public TestEmail Email { get; init; }
				}
				public sealed class Person : INorseEntity<Person>
				{
					[RetentionPolicy(RetentionBasis.SubjectKey)]
					public ContactCard Contact { get; init; } = null!;
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetentionPolicyAnalyzer(), EntityStub, PiiFixture, source);
		diagnostics.ShouldContain(d => d.Id == "NORSE062");
	}

	[Fact]
	async Task Reports_norse062_when_pii_is_a_collection_element()
	{
		var source =
			"""
			using System.Collections.Generic;
			using Fixtures;
			using Norse.Persistence.EntityFramework;
			namespace App
			{
				public sealed class Person : INorseEntity<Person>
				{
					public ICollection<TestEmail> Emails { get; init; } = [];
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetentionPolicyAnalyzer(), EntityStub, PiiFixture, source);
		diagnostics.ShouldContain(d => d.Id == "NORSE062");
	}

	[Fact]
	async Task Reports_norse062_not_norse061_when_pii_is_an_array_element()
	{
		// Arrays route through IArrayTypeSymbol, not INamedTypeSymbol — a named-type-only collection
		// guard misroutes this to the attribute-curable NORSE061. Banned means banned: NORSE062.
		var source =
			"""
			using Fixtures;
			using Norse.Persistence.EntityFramework;
			using Norse.Primitives.Pii;
			namespace App
			{
				public sealed class Person : INorseEntity<Person>
				{
					[RetentionPolicy(RetentionBasis.SubjectKey)]
					public TestEmail[] Emails { get; init; } = [];
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetentionPolicyAnalyzer(), EntityStub, PiiFixture, source);
		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE062"); // the attribute on the property does NOT cure it
	}

	[Fact]
	async Task Reports_nothing_when_pii_lives_on_a_type_that_is_not_a_persisted_root()
	{
		// Retention is a storage concern — a wire DTO holding PII transiently needs no basis.
		var source =
			"""
			using Fixtures;
			namespace App
			{
				public sealed class LoginRequest
				{
					public TestEmail Email { get; init; }
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetentionPolicyAnalyzer(), EntityStub, PiiFixture, source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Removing_the_attribute_from_a_declared_entity_fails_the_build()
	{
		// The spec §2a "wired, not just designed" fixture: same entity, attribute stripped → error.
		var source =
			"""
			using Fixtures;
			using Norse.Persistence.EntityFramework;
			namespace App
			{
				public sealed class NorseUserProfile : INorseEntity<NorseUserProfile>
				{
					public TestEmail RecoveryEmail { get; init; }
				}
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetentionPolicyAnalyzer(), EntityStub, PiiFixture, source);
		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE061");
		diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
	}
}
