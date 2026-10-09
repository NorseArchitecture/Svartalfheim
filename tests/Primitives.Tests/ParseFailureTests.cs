using HyperCast;

namespace Norse.Primitives.Tests;

public sealed class ParseFailureTests
{
	// Engines-cut spec §4.1: the four shared members are CastFailure by number, so the engine-to-forge
	// map is a reinterpret. A drift on either side fails here before it can fail in a switch.
	[Theory]
	[InlineData(ParseFailure.Unspecified, CastFailure.Unspecified)]
	[InlineData(ParseFailure.Empty, CastFailure.Empty)]
	[InlineData(ParseFailure.Malformed, CastFailure.Malformed)]
	[InlineData(ParseFailure.OutOfRange, CastFailure.OutOfRange)]
	void Should_mirror_cast_failure_numerically(ParseFailure reason, CastFailure cast) =>
		((byte)reason).ShouldBe((byte)cast);

	[Fact]
	void Should_keep_duplicate_as_the_forge_s_own_member_past_the_shared_four() =>
		((byte)ParseFailure.Duplicate).ShouldBe((byte)4);
}
