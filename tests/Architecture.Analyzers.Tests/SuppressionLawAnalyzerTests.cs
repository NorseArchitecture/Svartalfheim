namespace Norse.Architecture.Analyzers.Tests;

public sealed class SuppressionLawAnalyzerTests
{
	const string MemberLevelSuppression =
		"""
		using System.Diagnostics.CodeAnalysis;
		using System.Text.Json;

		namespace App;

		static class Leak
		{
			[SuppressMessage("Norse.Architecture", "NORSE070")]
			public static string Emit(object value) =>
				JsonSerializer.Serialize(value);
		}
		""";

	const string AssemblyLevelSuppression =
		"""
		using System.Diagnostics.CodeAnalysis;

		[assembly: SuppressMessage("Norse.Architecture", "NORSE071", Justification = "test fixture")]

		namespace App;

		static class Anchor;
		""";

	const string RecursiveSuppression =
		"""
		using System.Diagnostics.CodeAnalysis;

		namespace App;

		static class Leak
		{
			[SuppressMessage("Norse.Architecture", "NORSE079")]
			[SuppressMessage("Norse.Architecture", "NORSE070")]
			public static void Noop()
			{
			}
		}
		""";

	const string UnrelatedSuppression =
		"""
		using System.Diagnostics.CodeAnalysis;

		namespace App;

		static class Leak
		{
			[SuppressMessage("Performance", "CA1822")]
			public static void Noop()
			{
			}
		}
		""";

	[Fact]
	async Task Strikes_norse079_on_a_member_level_suppression_regardless_of_whether_the_suppressed_strike_survives()
	{
		// Run BOTH analyzers together — NORSE079 must convict whether or not NORSE070 itself
		// gets erased by the SuppressMessageAttribute it's carried on.
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			[new SuppressionLawAnalyzer(), new WireFormatAnalyzer()], "Norse.Identity.Web.Server", [], MemberLevelSuppression);
		diagnostics.ShouldContain(d => d.Id == "NORSE079");
	}

	[Fact]
	async Task Strikes_norse079_on_an_assembly_level_suppression()
	{
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new SuppressionLawAnalyzer(), "Norse.Identity.Web.Server", [], AssemblyLevelSuppression);
		diagnostics.ShouldContain(d => d.Id == "NORSE079");
	}

	[Fact]
	async Task Suppressing_norse079_itself_is_convicted_recursively()
	{
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new SuppressionLawAnalyzer(), "Norse.Identity.Web.Server", [], RecursiveSuppression);
		diagnostics.Count(d => d.Id == "NORSE079").ShouldBe(2);
	}

	[Fact]
	async Task Unrelated_suppression_ids_never_strike()
	{
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new SuppressionLawAnalyzer(), "Norse.Identity.Web.Server", [], UnrelatedSuppression);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Stays_silent_in_an_exempt_named_compilation()
	{
		var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
			new SuppressionLawAnalyzer(), "Norse.Identity.Web.Server.Tests", [], MemberLevelSuppression);
		diagnostics.ShouldBeEmpty();
	}
}
