namespace Norse.Primitives.Tests;

public sealed class TimeZoneParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	[Fact]
	void Should_resolve_value_when_iana_id_is_recognized()
	{
		var actual = TimeZoneParser.ParseRequired("America/Chicago");
		actual.TryGetValue(out Success<TimeZoneInfo> success).ShouldBeTrue();
		success.Value.Id.ShouldBe("America/Chicago");
	}

	[Fact]
	void Should_trim_surrounding_whitespace_before_resolving()
	{
		var actual = TimeZoneParser.ParseRequired("  America/New_York  ");
		actual.TryGetValue(out Success<TimeZoneInfo> success).ShouldBeTrue();
		success.Value.Id.ShouldBe("America/New_York");
	}

	[Theory]
	[InlineData("Not/A/Zone")]
	[InlineData("garbage")]
	[InlineData("America/Bogus")]
	void Should_fail_with_malformed_reason_when_iana_id_is_unrecognized(string input)
	{
		var actual = TimeZoneParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("TimeZoneInfo");
		failure.Format.ShouldBe("IANA");
		failure.Detail.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = TimeZoneParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.Input.ShouldBe(string.Empty);
		failure.ExpectedType.ShouldBe("TimeZoneInfo");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		TimeZoneParser.ParseOptional(input).HasValue.ShouldBeFalse();

	[Fact]
	void Should_resolve_value_when_optional_input_is_recognized()
	{
		var actual = TimeZoneParser.ParseOptional("Europe/London");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<TimeZoneInfo> success).ShouldBeTrue();
		success.Value.Id.ShouldBe("Europe/London");
	}

	[Fact]
	void Should_not_fall_back_to_utc_when_id_is_absent()
	{
		// Absence is absence — not a fallback to UTC or the local zone.
		var actual = TimeZoneParser.ParseRequired("");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
	}

	[Fact]
	void Should_fail_with_malformed_reason_when_optional_input_is_unrecognized()
	{
		var actual = TimeZoneParser.ParseOptional("Not/A/Zone");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("TimeZoneInfo");
		failure.Format.ShouldBe("IANA");
	}
}
