namespace Norse.Primitives.Tests;

public sealed class BooleanParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	[Theory]
	[InlineData("true")]
	[InlineData("True")]
	[InlineData("TRUE")]
	[InlineData("t")]
	[InlineData("T")]
	[InlineData("false", false)]
	[InlineData("False", false)]
	[InlineData("FALSE", false)]
	[InlineData("f", false)]
	[InlineData("F", false)]
	[InlineData("yes")]
	[InlineData("Yes")]
	[InlineData("YES")]
	[InlineData("y")]
	[InlineData("Y")]
	[InlineData("no", false)]
	[InlineData("No", false)]
	[InlineData("NO", false)]
	[InlineData("n", false)]
	[InlineData("N", false)]
	[InlineData("1")]
	[InlineData("0", false)]
	[InlineData("on")]
	[InlineData("On")]
	[InlineData("ON")]
	[InlineData("off", false)]
	[InlineData("Off", false)]
	[InlineData("OFF", false)]
	[InlineData("enabled")]
	[InlineData("Enabled")]
	[InlineData("ENABLED")]
	[InlineData("disabled", false)]
	[InlineData("Disabled", false)]
	[InlineData("DISABLED", false)]
	[InlineData("active")]
	[InlineData("Active")]
	[InlineData("inactive", false)]
	[InlineData("InAcTiVe", false)]
	[InlineData("checked")]
	[InlineData("CheckeD")]
	[InlineData("unchecked", false)]
	[InlineData("UnchEcked", false)]
	[InlineData("in")]
	[InlineData("In")]
	[InlineData("out", false)]
	[InlineData("Out", false)]
	[InlineData("\ttrue\n")]
	[InlineData("  Y  ")]
	[InlineData("tRuE")]
	[InlineData("fAlSe", false)]
	[InlineData("true\0")]
	[InlineData(" Y ")]
	void Should_parse_value_when_input_is_recognized(string input, bool expected = true)
	{
		var actual = BooleanParser.ParseRequired(input);
		actual.TryGetValue(out Success<bool> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Theory]
	[InlineData("yes")]
	[InlineData("0", false)]
	void Should_parse_value_when_optional_input_is_recognized(string input, bool expected = true)
	{
		var actual = BooleanParser.ParseOptional(input);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<bool> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	[InlineData(" ")]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = BooleanParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.Input.ShouldBe(string.Empty);
		failure.ExpectedType.ShouldBe("Boolean");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	[InlineData(" ")]
	void Should_return_absent_when_optional_input_is_absent(string? input)
	{
		var actual = BooleanParser.ParseOptional(input);
		actual.HasValue.ShouldBeFalse();
	}

	[Theory]
	[InlineData("invalid")]
	[InlineData("2")]
	[InlineData("maybe")]
	[InlineData("unknown")]
	// ReSharper disable StringLiteralTypo
	[InlineData("truee")]
	[InlineData("\tyess\n")]
	// ReSharper restore StringLiteralTypo
	[InlineData("yes\0")]
	void Should_fail_with_malformed_reason_when_input_is_unrecognized(string input)
	{
		var actual = BooleanParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.Input.ShouldBe(input.Trim());
		failure.ExpectedType.ShouldBe("Boolean");
		failure.Format.ShouldBeNull();
		failure.Detail.ShouldBeNull();
	}

	[Fact]
	void Should_fail_with_malformed_reason_when_optional_input_is_unrecognized()
	{
		var actual = BooleanParser.ParseOptional("maybe");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_truncate_captured_input_when_malformed_input_is_oversized()
	{
		var oversized = new string('x', Failure.MaxInputLength + 100);
		var actual = BooleanParser.ParseRequired(oversized);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.Input.Length.ShouldBe(Failure.MaxInputLength);
	}
}
