using HyperUuid;
using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Tests.Identifiers;

public sealed class GuidByteOrderTests
{
	[Fact]
	void Should_have_fixed_numeric_values_when_enum_is_inspected()
	{
		// Explicit values are load-bearing: this enum is expected to eventually appear as a
		// persisted integer, and an accidental renumbering must be a visible diff, not a silent bug.
		((int)GuidByteOrder.Unspecified).ShouldBe(0);
		((int)GuidByteOrder.Rfc9562).ShouldBe(1);
		((int)GuidByteOrder.SqlServer).ShouldBe(2);
	}

	// Engines-cut spec §5: the cast between the forge's byte-order tag and the engine's layout is a reinterpret.
	[Theory]
	[InlineData(GuidByteOrder.Unspecified, UuidLayout.Unspecified)]
	[InlineData(GuidByteOrder.Rfc9562, UuidLayout.Rfc9562)]
	[InlineData(GuidByteOrder.SqlServer, UuidLayout.SqlServer)]
	void Should_mirror_uuid_layout_numerically(GuidByteOrder order, UuidLayout layout) =>
		((uint)order).ShouldBe((uint)layout);
}
