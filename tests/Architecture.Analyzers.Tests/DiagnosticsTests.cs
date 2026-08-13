using Microsoft.CodeAnalysis;

namespace Norse.Architecture.Analyzers.Tests;

public sealed class DiagnosticsTests
{
	[Theory]
	[InlineData("NORSE070")]
	[InlineData("NORSE071")]
	[InlineData("NORSE072")]
	[InlineData("NORSE073")]
	[InlineData("NORSE074")]
	[InlineData("NORSE075")]
	[InlineData("NORSE079")]
	void Every_strike_is_a_non_configurable_error(string id)
	{
		var descriptor = All().Single(d => d.Id == id);
		descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error);
		descriptor.IsEnabledByDefault.ShouldBeTrue();
		descriptor.CustomTags.ShouldContain(WellKnownDiagnosticTags.NotConfigurable);
		descriptor.Category.ShouldBe("Norse.Architecture");
	}

	static DiagnosticDescriptor[] All() =>
		[Diagnostics.WireFormatOutsideBorder, Diagnostics.MidgardTakenAsDependency, Diagnostics.CrossRealmReach, Diagnostics.ComponentImpurity, Diagnostics.ForcedLoadOutsideTheGate, Diagnostics.ValidSubmitOnSeamBoundForm, Diagnostics.SuppressingTheLaw];
}
