namespace Norse.Primitives.Tests;

public sealed class ParseFailureTests
{
	[Theory]
	[InlineData(ParseFailure.Unspecified, 0)]
	[InlineData(ParseFailure.Empty, 1)]
	[InlineData(ParseFailure.Malformed, 2)]
	[InlineData(ParseFailure.OutOfRange, 3)]
	[InlineData(ParseFailure.Duplicate, 4)]
	void Values_mirror_HyperCasts_CastFailure_for_the_shared_cases(ParseFailure failure, byte expected) =>
		((byte)failure).ShouldBe(expected);
}
