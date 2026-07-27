namespace Norse.Primitives.Tests;

public sealed class FailureTests
{
	[Fact]
	void Should_pass_input_through_when_within_bound()
	{
		Failure failure = new(ParseFailure.Malformed, "bogus", "Boolean");
		failure.Input.ShouldBe("bogus");
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("Boolean");
		failure.Format.ShouldBeNull();
		failure.Detail.ShouldBeNull();
	}

	[Fact]
	void Should_truncate_input_when_longer_than_max()
	{
		string oversized = new('x', Failure.MaxInputLength + 44);
		Failure failure = new(ParseFailure.Malformed, oversized, "Boolean");
		failure.Input.Length.ShouldBe(Failure.MaxInputLength);
		failure.Input.ShouldBe(oversized[..Failure.MaxInputLength]);
	}

	[Fact]
	void Should_truncate_span_input_when_longer_than_max()
	{
		string oversized = new('x', Failure.MaxInputLength + 44);
		Failure failure = new(ParseFailure.Malformed, oversized.AsSpan(), "Boolean");
		failure.Input.Length.ShouldBe(Failure.MaxInputLength);
		failure.Input.ShouldBe(oversized[..Failure.MaxInputLength]);
	}

	[Fact]
	void Should_be_equal_when_all_fields_match()
	{
		Failure
			left = new(ParseFailure.Empty, "", "Boolean"),
			right = new(ParseFailure.Empty, "", "Boolean");
		left.ShouldBe(right);
	}

	[Fact]
	void Should_not_be_equal_when_reason_differs()
	{
		Failure
			left = new(ParseFailure.Empty, "", "Boolean"),
			right = new(ParseFailure.Malformed, "", "Boolean");
		left.ShouldNotBe(right);
	}

	[Theory]
	[InlineData(ParseFailure.Unspecified)]
	[InlineData((ParseFailure)99)]
	void Should_throw_when_reason_is_not_a_real_failure(ParseFailure reason) =>
		Should.Throw<ArgumentOutOfRangeException>(() => new Failure(reason, "x", "Boolean"));

	[Fact]
	void Should_throw_when_input_is_null() =>
		Should.Throw<ArgumentNullException>(() => new Failure(ParseFailure.Malformed, null!, "Boolean"));

	[Theory]
	[InlineData("")]
	[InlineData(" \t ")]
	void Should_throw_when_expected_type_is_missing(string expectedType) =>
		Should.Throw<ArgumentException>(() => new Failure(ParseFailure.Malformed, "x", expectedType));

	[Fact]
	void Should_be_equal_when_format_and_detail_match()
	{
		Failure
			left = new(ParseFailure.Malformed, "x", "DateOnly", "yyyy-MM-dd", "detail"),
			right = new(ParseFailure.Malformed, "x", "DateOnly", "yyyy-MM-dd", "detail");
		left.ShouldBe(right);
	}

	[Fact]
	void Should_not_be_equal_when_format_differs()
	{
		Failure
			left = new(ParseFailure.Malformed, "x", "DateOnly", "yyyy-MM-dd"),
			right = new(ParseFailure.Malformed, "x", "DateOnly", "MM/dd/yyyy");
		left.ShouldNotBe(right);
	}

	[Fact]
	void Should_not_be_equal_when_detail_differs()
	{
		Failure
			left = new(ParseFailure.Malformed, "x", "Boolean", null, "left"),
			right = new(ParseFailure.Malformed, "x", "Boolean", null, "right");
		left.ShouldNotBe(right);
	}

	[Fact]
	void Should_expose_sentinel_state_when_defaulted()
	{
		// Canary documenting the struct-default footgun: default(Failure) bypasses
		// every constructor guard and is not a valid value.
		var defaulted = default(Failure);
		defaulted.Reason.ShouldBe(ParseFailure.Unspecified);
		defaulted.Input.ShouldBeNull();
		defaulted.ExpectedType.ShouldBeNull();
	}
}
