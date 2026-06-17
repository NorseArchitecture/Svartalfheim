namespace Norse.Primitives.Tests;

public sealed class CharParserTests
{
	[Theory]
	[InlineData("A", 'A')]
	[InlineData("7", '7')]       // a single char is itself, never code point 7
	[InlineData(" ", ' ')]       // a literal space is preserved, not trimmed away
	[InlineData("\t", '\t')]
	[InlineData("65", 'A')]      // decimal code point
	[InlineData("  65  ", 'A')]  // surrounding whitespace trimmed for the multi-char form
	[InlineData("0x41", 'A')]    // hex code point
	[InlineData("&H41", 'A')]
	[InlineData("U+0041", 'A')]
	[InlineData("&#65;", 'A')]   // HTML entity, decimal
	[InlineData("&#x41;", 'A')]  // HTML entity, hex
	void Should_parse_value_when_char_input_is_recognized(string input, char expected)
	{
		var actual = CharParser.ParseRequired(input);
		actual.TryGetValue(out Success<char> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Theory]
	[InlineData("70000")]   // beyond the UTF-16 range 0..65535
	[InlineData("-5")]      // negative code point
	[InlineData("AB")]      // two literal chars, no coded form
	[InlineData("0xZZ")]
	[InlineData("&#70000;")]
	void Should_fail_with_malformed_reason_when_char_input_is_unrecognized(string input)
	{
		var actual = CharParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("Char");
		failure.Format.ShouldBeNull();
		failure.Detail.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = CharParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.Input.ShouldBe(string.Empty);
		failure.ExpectedType.ShouldBe("Char");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		CharParser.ParseOptional(input).HasValue.ShouldBeFalse();

	[Fact]
	void Should_preserve_literal_space_when_optional()
	{
		var actual = CharParser.ParseOptional(" ");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<char> success).ShouldBeTrue();
		success.Value.ShouldBe(' ');
	}

	[Fact]
	void Should_parse_value_when_optional_input_is_recognized()
	{
		var actual = CharParser.ParseOptional("&#65;");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<char> success).ShouldBeTrue();
		success.Value.ShouldBe('A');
	}
}
