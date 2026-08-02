using Microsoft.CodeAnalysis;

namespace Norse.Primitives.Analyzers.Tests;

/// <summary>
/// NORSE060 — <c>Result&lt;T&gt;</c> reachable from a <c>[ServiceContract]</c>/<c>[OperationContract]</c>
/// method's response payload. One test per reachability path the law covers (top-level, nested complex
/// member, collection item type, nullable), the two legitimate non-firing shapes (request-only,
/// clean-pass), the message-content contract, and the two defensive properties (cyclic graph, diamond
/// reachability) called out in the design's self-review bar.
/// </summary>
public sealed class ResultInServiceResponseAnalyzerTests
{
	[Fact]
	async Task Fires_on_a_top_level_Result_property_in_the_response_payload()
	{
		const string Fixture = """
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;
			using Norse.Primitives;

			namespace Norse.Fixtures.TopLevel;

			[DataContract]
			public sealed record BadResponse
			{
				[DataMember(Order = 1)]
				public Result<int> Total { get; init; }
			}

			[ServiceContract(Name = "grpc.fixtures.v1.BadService")]
			public interface IBadService
			{
				[OperationContract]
				Task<Outcome<BadResponse>> DoAsync(CancellationToken cancellationToken = default);
			}
			""";

		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE060");
		diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
		SourceAt(Fixture, diagnostic).ShouldBe("Total");
	}

	[Fact]
	async Task Fires_on_a_nullable_Result_property_in_the_response_payload()
	{
		const string Fixture = """
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;
			using Norse.Primitives;

			namespace Norse.Fixtures.NullableTopLevel;

			[DataContract]
			public sealed record BadResponse
			{
				[DataMember(Order = 1)]
				public Result<int>? Total { get; init; }
			}

			[ServiceContract(Name = "grpc.fixtures.v1.BadService")]
			public interface IBadService
			{
				[OperationContract]
				ValueTask<Outcome<BadResponse>> DoAsync(CancellationToken cancellationToken = default);
			}
			""";

		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE060");
		SourceAt(Fixture, diagnostic).ShouldBe("Total");
	}

	[Fact]
	async Task Fires_on_a_Result_property_reachable_through_a_nested_complex_member()
	{
		const string Fixture = """
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;
			using Norse.Primitives;

			namespace Norse.Fixtures.NestedMember;

			[DataContract]
			public sealed record Inner
			{
				[DataMember(Order = 1)]
				public Result<decimal> Amount { get; init; }
			}

			[DataContract]
			public sealed record OuterResponse
			{
				[DataMember(Order = 1)]
				public Inner Detail { get; init; } = null!;
			}

			[ServiceContract(Name = "grpc.fixtures.v1.NestedService")]
			public interface INestedService
			{
				[OperationContract]
				Task<Outcome<OuterResponse>> DoAsync(CancellationToken cancellationToken = default);
			}
			""";

		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE060");
		SourceAt(Fixture, diagnostic).ShouldBe("Amount");
	}

	[Fact]
	async Task Fires_on_a_Result_property_reachable_through_a_collection_item_type()
	{
		const string Fixture = """
			using System.Collections.Generic;
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;
			using Norse.Primitives;

			namespace Norse.Fixtures.CollectionItem;

			[DataContract]
			public sealed record LineItem
			{
				[DataMember(Order = 1)]
				public Result<decimal> Price { get; init; }
			}

			[DataContract]
			public sealed record OrderResponse
			{
				[DataMember(Order = 1)]
				public List<LineItem> Items { get; init; } = new();
			}

			[ServiceContract(Name = "grpc.fixtures.v1.OrderService")]
			public interface IOrderService
			{
				[OperationContract]
				Task<Outcome<OrderResponse>> DoAsync(CancellationToken cancellationToken = default);
			}
			""";

		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE060");
		SourceAt(Fixture, diagnostic).ShouldBe("Price");
	}

	[Fact]
	async Task Does_not_fire_when_Result_appears_only_on_the_request_parameter_type()
	{
		const string Fixture = """
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;
			using Norse.Primitives;

			namespace Norse.Fixtures.RequestOnly;

			[DataContract]
			public sealed record GoodRequest
			{
				[DataMember(Order = 1)]
				public Result<decimal> Limit { get; init; }
			}

			[DataContract]
			public sealed record GoodResponse
			{
				[DataMember(Order = 1)]
				public decimal Total { get; init; }
			}

			[ServiceContract(Name = "grpc.fixtures.v1.GoodService")]
			public interface IGoodService
			{
				[OperationContract]
				Task<Outcome<GoodResponse>> DoAsync(GoodRequest request, CancellationToken cancellationToken = default);
			}
			""";

		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Does_not_fire_when_no_Result_is_reachable_from_the_response()
	{
		const string Fixture = """
			using System.Collections.Generic;
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;

			namespace Norse.Fixtures.CleanPass;

			[DataContract]
			public sealed record Address
			{
				[DataMember(Order = 1)]
				public string Line1 { get; init; } = "";
			}

			[DataContract]
			public sealed record CleanResponse
			{
				[DataMember(Order = 1)]
				public decimal Total { get; init; }

				[DataMember(Order = 2)]
				public Address Billing { get; init; } = null!;

				[DataMember(Order = 3)]
				public List<Address> ShippingHistory { get; init; } = new();
			}

			[ServiceContract(Name = "grpc.fixtures.v1.CleanService")]
			public interface ICleanService
			{
				[OperationContract]
				Task<Outcome<CleanResponse>> DoAsync(CancellationToken cancellationToken = default);
			}
			""";

		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Does_not_infinite_loop_on_a_cyclic_type_graph()
	{
		const string Fixture = """
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;

			namespace Norse.Fixtures.Cyclic;

			[DataContract]
			public sealed record NodeA
			{
				[DataMember(Order = 1)]
				public NodeB Next { get; init; } = null!;
			}

			[DataContract]
			public sealed record NodeB
			{
				[DataMember(Order = 1)]
				public NodeA Next { get; init; } = null!;
			}

			[ServiceContract(Name = "grpc.fixtures.v1.CyclicService")]
			public interface ICyclicService
			{
				[OperationContract]
				Task<Outcome<NodeA>> DoAsync(CancellationToken cancellationToken = default);
			}
			""";

		// The assertion IS that this returns at all — a real infinite loop hangs the test run rather
		// than failing an assertion, which is exactly why this scenario earns its own test.
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Fires_once_per_violating_property_even_when_the_declaring_type_is_reachable_through_two_paths()
	{
		const string Fixture = """
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;
			using Norse.Primitives;

			namespace Norse.Fixtures.Diamond;

			[DataContract]
			public sealed record Shared
			{
				[DataMember(Order = 1)]
				public Result<int> Value { get; init; }
			}

			[DataContract]
			public sealed record DiamondResponse
			{
				[DataMember(Order = 1)]
				public Shared Left { get; init; } = null!;

				[DataMember(Order = 2)]
				public Shared Right { get; init; } = null!;
			}

			[ServiceContract(Name = "grpc.fixtures.v1.DiamondService")]
			public interface IDiamondService
			{
				[OperationContract]
				Task<Outcome<DiamondResponse>> DoAsync(CancellationToken cancellationToken = default);
			}
			""";

		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		// Shared is one visited node in the walk (Left and Right both point at the exact same named
		// type), so its Value property is reported once, not twice.
		var diagnostic = diagnostics.ShouldHaveSingleItem();
		diagnostic.Id.ShouldBe("NORSE060");
		SourceAt(Fixture, diagnostic).ShouldBe("Value");
	}

	[Fact]
	async Task Diagnostic_message_names_the_property_declaring_type_and_exposing_ServiceContract_method()
	{
		const string Fixture = """
			using System.Runtime.Serialization;
			using System.ServiceModel;
			using System.Threading;
			using System.Threading.Tasks;
			using Norse.Abstractions.Contracts;
			using Norse.Primitives;

			namespace Norse.Fixtures.Message;

			[DataContract]
			public sealed record BadResponse
			{
				[DataMember(Order = 1)]
				public Result<int> Total { get; init; }
			}

			[ServiceContract(Name = "grpc.fixtures.v1.MessageService")]
			public interface IMessageService
			{
				[OperationContract]
				Task<Outcome<BadResponse>> DoAsync(CancellationToken cancellationToken = default);
			}
			""";

		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(Fixture);

		var diagnostic = diagnostics.ShouldHaveSingleItem();
		var message = diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
		message.ShouldContain("Total");
		message.ShouldContain("Norse.Fixtures.Message.BadResponse");
		message.ShouldContain("Norse.Fixtures.Message.IMessageService");
		message.ShouldContain("DoAsync");
	}

	/// <summary>The exact source substring at <paramref name="diagnostic"/>'s reported span, within <paramref name="fixture"/> — proves the squiggle lands on the offending property, not merely that the right ID fired.</summary>
	static string SourceAt(string fixture, Diagnostic diagnostic)
	{
		var span = diagnostic.Location.SourceSpan;
		return fixture.Substring(span.Start, span.Length);
	}
}
