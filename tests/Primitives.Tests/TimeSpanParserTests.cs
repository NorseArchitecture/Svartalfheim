using System.Globalization;

namespace Norse.Primitives.Tests;

public sealed class TimeSpanParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Theory]
	[InlineData("01:30:00")]        // 1h30m, colon form
	[InlineData("1.06:00:00")]      // 1d6h
	[InlineData("PT1H30M")]         // ISO 8601 duration
	[InlineData("P1DT6H")]          // 1d6h ISO duration
	void Should_parse_value_when_duration_is_recognized(string input)
	{
		var actual = TimeSpanParser.ParseRequired(input);
		actual.TryGetValue(out Success<TimeSpan> _).ShouldBeTrue();
	}

	[Fact]
	void Should_parse_colon_and_iso_to_the_same_span()
	{
		TimeSpanParser.ParseRequired("01:30:00").TryGetValue(out Success<TimeSpan> colon).ShouldBeTrue();
		TimeSpanParser.ParseRequired("PT1H30M").TryGetValue(out Success<TimeSpan> iso).ShouldBeTrue();
		colon.Value.ShouldBe(new TimeSpan(1, 30, 0));
		iso.Value.ShouldBe(new TimeSpan(1, 30, 0));
	}

	[Fact]
	void Should_parse_iso_weeks_designator()
	{
		TimeSpanParser.ParseRequired("P2W").TryGetValue(out Success<TimeSpan> weeks).ShouldBeTrue();
		weeks.Value.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	void Should_parse_iso_fractional_seconds()
	{
		TimeSpanParser.ParseRequired("PT1.5S").TryGetValue(out Success<TimeSpan> frac).ShouldBeTrue();
		frac.Value.ShouldBe(TimeSpan.FromSeconds(1.5));
	}

	[Fact]
	void Should_parse_negative_iso_duration()
	{
		TimeSpanParser.ParseRequired("-PT1H").TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.ShouldBe(TimeSpan.FromHours(-1));
	}

	[Fact]
	void Should_accept_zero_as_valid()
	{
		TimeSpanParser.ParseRequired("00:00:00").TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.ShouldBe(TimeSpan.Zero);
	}

	[Theory]
	[InlineData("P1Y")]   // years are not fixed durations
	[InlineData("P2M")]   // months (before T) are not fixed durations
	[InlineData("P")]     // no component
	[InlineData("PT")]    // T with no time component
	[InlineData("P3DT")]  // trailing T with no time component
	[InlineData("PT1H30")] // number with no unit
	[InlineData("90m")]   // bare unit shorthand not supported
	[InlineData("garbage")]
	void Should_fail_with_malformed_reason_when_duration_is_unrecognized(string input)
	{
		var actual = TimeSpanParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("TimeSpan");
	}

	[Fact]
	void Should_honor_declared_format_on_the_exact_door()
	{
		TimeSpanParser.ParseExactRequired("01:30", @"hh\:mm", _invariant)
			.TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.ShouldBe(new TimeSpan(1, 30, 0));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.ExpectedType.ShouldBe("TimeSpan");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		TimeSpanParser.ParseOptional(input).HasValue.ShouldBeFalse();

	[Theory]
	[InlineData("10675199.02:48:05.4775807")]   // TimeSpan.MaxValue round-trip
	[InlineData("-10675199.02:48:05.4775808")]  // TimeSpan.MinValue round-trip
	void Should_reject_sentinel_spans_as_malformed(string input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Theory]
	[InlineData("PT999999999999999999S")]   // would overflow the decimal->long cast
	[InlineData("PT9999999999999999H")]     // would silently wrap long ticks
	[InlineData("P9999999999999999W")]
	void Should_fail_with_malformed_reason_when_iso_duration_overflows(string input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}
}
