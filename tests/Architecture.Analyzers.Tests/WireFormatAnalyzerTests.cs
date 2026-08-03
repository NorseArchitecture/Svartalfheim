using Microsoft.CodeAnalysis;

namespace Norse.Architecture.Analyzers.Tests;

public sealed class WireFormatAnalyzerTests
{
	const string GuiltySerialize =
		"""
		using System.Text.Json;

		namespace App;

		static class Leak
		{
			public static string Emit(object value) =>
				JsonSerializer.Serialize(value);
		}
		""";

	[Fact]
	async Task Strikes_norse070_on_a_banned_using_in_a_realm_assembly()
	{
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], GuiltySerialize);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}

	[Fact]
	async Task Stays_silent_for_the_same_code_inside_the_wire_border()
	{
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Infrastructure.Web.Server", [], GuiltySerialize);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Stays_silent_for_hosting_and_for_exempt_assemblies()
	{
		(await AnalyzerTestHarness.GetDiagnosticsAsync(new WireFormatAnalyzer(), "Norse.Hosting.Web.Server", [], GuiltySerialize))
			.ShouldBeEmpty();
		(await AnalyzerTestHarness.GetDiagnosticsAsync(new WireFormatAnalyzer(), "Norse.Identity.Web.Server.Tests", [], GuiltySerialize))
			.ShouldBeEmpty();
	}

	[Fact]
	async Task Strikes_the_brand_blind_anchorless_contracts_assembly()
	{
		// Spec §3 brand-blind ruling: no vocabulary segment, no governed references, still convicted.
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Contracts", [], GuiltySerialize);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}

	[Fact]
	async Task Blesses_contract_attributes_as_declarations_of_intent()
	{
		var source =
			"""
			using System.Runtime.Serialization;

			namespace App;

			[DataContract]
			public sealed record LoginRequest
			{
				[DataMember(Order = 1)]
				public string Email { get; set; } = "";
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Contracts", [], source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Strikes_a_fully_qualified_use_with_no_using_directive()
	{
		var source =
			"""
			namespace App;

			static class Leak
			{
				public static string Emit(object value) =>
					System.Text.Json.JsonSerializer.Serialize(value);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}

	[Fact]
	async Task Strikes_an_alias_laundered_use()
	{
		var source =
			"""
			using Codec = System.Text.Json.JsonSerializer;

			namespace App;

			static class Leak
			{
				public static string Emit(object value) =>
					Codec.Serialize(value);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}

	[Fact]
	async Task Strikes_a_fully_qualified_type_in_a_declaration_context()
	{
		// A field declaration produces QualifiedNameSyntax — no operation fires for it, so this
		// is the one layer-2 proof: delete AnalyzeQualifiedName and this test goes red.
		var source =
			"""
			namespace App;

			sealed class Holder
			{
				System.Text.Json.JsonSerializerOptions? _options;

				public object? Peek() =>
					_options;
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}

	[Fact]
	async Task Dedupes_a_fully_qualified_new_of_a_banned_type()
	{
		// new System.Text.Json.JsonSerializerOptions() should fire exactly ONE NORSE070,
		// not two (QualifiedName layer + Operation layer). The operation layer owns the report
		// for object creations; QualifiedName skips when the parent is ObjectCreationExpressionSyntax.
		var source =
			"""
			namespace App;

			sealed class Holder
			{
				public object? Create() =>
					new System.Text.Json.JsonSerializerOptions();
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], source);
		diagnostics.Count(d => d.Id == "NORSE070").ShouldBe(1);
	}

	[Fact]
	async Task Strikes_the_banned_typed_results_json_symbol()
	{
		// Results.Json/TypedResults.Json live in innocent namespaces — symbol-level ban (spec §4).
		// Stub carries the real metadata name so no ASP.NET shared-framework reference is needed.
		const string TypedResultsStub =
			"""
			namespace Microsoft.AspNetCore.Http
			{
				public static class TypedResults
				{
					public static object Json(object? value) => new();
				}
			}
			""";
		var source =
			"""
			using Microsoft.AspNetCore.Http;

			namespace App;

			static class Leak
			{
				public static object Emit(object value) =>
					TypedResults.Json(value);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], TypedResultsStub, source);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}

	[Fact]
	async Task Survives_a_pragma_suppression_attempt()
	{
		// Spec §7 suppression-proofing: NotConfigurable must hold against #pragma. If this test
		// fails RED because the pragma pierces, implement the Location.None compilation-end backstop
		// described in the task notes — the assertion below stays the authority either way.
		var source =
			"""
			#pragma warning disable NORSE070
			using System.Text.Json;
			#pragma warning restore NORSE070

			namespace App;

			static class Leak
			{
				public static string Emit(object value) =>
					JsonSerializer.Serialize(value);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], source);
		diagnostics.Where(d => d.Id == "NORSE070" && !d.IsSuppressed).ShouldNotBeEmpty();
	}

	[Fact]
	async Task Stays_silent_for_a_banned_type_named_only_in_a_doc_comment_cref()
	{
		// DocumentationMode.Diagnose so the parser actually produces the cref's structured-trivia
		// QualifiedNameSyntax under test — the harness default (Parse) does not bind/expose it the same
		// way and would make this fixture pass for the wrong reason (node never visited at all).
		var source =
			"""
			namespace App;

			/// <summary>See <see cref="System.Xml.XmlReader"/>.</summary>
			sealed class Innocent
			{
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", AnalyzerTestHarness.ParseOptions.WithDocumentationMode(DocumentationMode.Diagnose), [], source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Strikes_norse070_on_a_global_qualified_field_declaration()
	{
		var source =
			"""
			namespace App;

			sealed class Holder
			{
				global::System.Text.Json.JsonSerializerOptions? _options;
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}

	[Fact]
	async Task Strikes_norse070_on_a_global_qualified_alias_using()
	{
		var source =
			"""
			using Codec = global::System.Text.Json.JsonSerializer;

			namespace App;

			static class Leak
			{
				public static string Emit(object value) =>
					Codec.Serialize(value);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}

	[Fact]
	async Task Strikes_exactly_one_norse070_on_a_bare_property_reference_with_no_invocation()
	{
		var source =
			"""
			namespace App;

			sealed class Holder
			{
				public object Peek() =>
					System.Text.Json.JsonSerializerOptions.Default;
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Identity.Web.Server", [], source);
		diagnostics.Count(d => d.Id == "NORSE070").ShouldBe(1);
	}

	[Fact]
	async Task Regression_the_forge_conviction_shape_strikes()
	{
		// Day-one conviction #1/#2 (spec §6): a JsonConverter living below the border.
		var source =
			"""
			using System;
			using System.Text.Json;
			using System.Text.Json.Serialization;

			namespace App;

			public sealed class MaskedValueJsonConverter : JsonConverter<int>
			{
				public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
					throw new NotSupportedException();

				public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
					writer.WriteNumberValue(value);
			}
			""";
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new WireFormatAnalyzer(), "Norse.Primitives", [], source);
		diagnostics.ShouldContain(d => d.Id == "NORSE070");
	}
}
