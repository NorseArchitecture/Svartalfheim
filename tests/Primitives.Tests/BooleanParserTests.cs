namespace Norse.Primitives.Tests;

// Runs in NativeCapabilityCollection: the "_on_the_forced_managed_path" theories below call
// NativeCapability.ForManagedOnly, which mutates thread-local state that must not race another
// test reading NativeCapability.Available concurrently.
[Collection(nameof(NativeCapabilityCollection))]
public sealed class BooleanParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	public static TheoryData<string, bool> RecognizedInputs => new()
	{
		{ "true", true },
		{ "True", true },
		{ "TRUE", true },
		{ "t", true },
		{ "T", true },
		{ "false", false },
		{ "False", false },
		{ "FALSE", false },
		{ "f", false },
		{ "F", false },
		{ "yes", true },
		{ "Yes", true },
		{ "YES", true },
		{ "y", true },
		{ "Y", true },
		{ "no", false },
		{ "No", false },
		{ "NO", false },
		{ "n", false },
		{ "N", false },
		{ "1", true },
		{ "0", false },
		{ "on", true },
		{ "On", true },
		{ "ON", true },
		{ "off", false },
		{ "Off", false },
		{ "OFF", false },
		{ "enabled", true },
		{ "Enabled", true },
		{ "ENABLED", true },
		{ "disabled", false },
		{ "Disabled", false },
		{ "DISABLED", false },
		{ "active", true },
		{ "Active", true },
		{ "inactive", false },
		{ "InAcTiVe", false },
		{ "checked", true },
		{ "CheckeD", true },
		{ "unchecked", false },
		{ "UnchEcked", false },
		{ "in", true },
		{ "In", true },
		{ "out", false },
		{ "Out", false },
		{ "\ttrue\n", true },
		{ "  Y  ", true },
		{ "tRuE", true },
		{ "fAlSe", false },
		{ " Y ", true },
	};

	[Theory]
	[MemberData(nameof(RecognizedInputs))]
	void Should_parse_value_when_input_is_recognized(string input, bool expected)
	{
		var actual = BooleanParser.ParseRequired(input);
		actual.TryGetValue(out Success<bool> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Theory]
	[MemberData(nameof(RecognizedInputs))]
	void Should_parse_value_when_input_is_recognized_on_the_forced_managed_path(string input, bool expected) =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = BooleanParser.ParseRequired(input);
			actual.TryGetValue(out Success<bool> success).ShouldBeTrue();
			success.Value.ShouldBe(expected);
		});

	public static TheoryData<string, bool> RecognizedOptionalInputs => new()
	{
		{ "yes", true },
		{ "0", false },
	};

	[Theory]
	[MemberData(nameof(RecognizedOptionalInputs))]
	void Should_parse_value_when_optional_input_is_recognized(string input, bool expected)
	{
		var actual = BooleanParser.ParseOptional(input);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<bool> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Theory]
	[MemberData(nameof(RecognizedOptionalInputs))]
	void Should_parse_value_when_optional_input_is_recognized_on_the_forced_managed_path(string input, bool expected) =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = BooleanParser.ParseOptional(input);
			actual.HasValue.ShouldBeTrue();
			actual.Value.TryGetValue(out Success<bool> success).ShouldBeTrue();
			success.Value.ShouldBe(expected);
		});

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
	// HyperCast's corpus is authoritative on a trailing NUL: it's Malformed, not a recognized
	// "true" literal. The prior managed-only leniency here came from bool.TryParse silently
	// trimming a trailing '\0' -- superseded now that the native path enforces the real grammar.
	[InlineData("true\0")]
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
		string oversized = new('x', Failure.MaxInputLength + 100);
		var actual = BooleanParser.ParseRequired(oversized);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.Input.Length.ShouldBe(Failure.MaxInputLength);
	}
}
