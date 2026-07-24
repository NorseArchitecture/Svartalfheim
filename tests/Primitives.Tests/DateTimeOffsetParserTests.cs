using System.Globalization;

namespace Norse.Primitives.Tests;

public sealed class DateTimeOffsetParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Theory]
	[InlineData("2026-01-02T15:04:05Z")]
	[InlineData("2026-01-02T15:04:05.123Z")]
	void Should_parse_utc_zone_to_zero_offset(string input)
	{
		var actual = DateTimeOffsetParser.ParseRequired(input);
		actual.TryGetValue(out Success<DateTimeOffset> success).ShouldBeTrue();
		success.Value.Offset.ShouldBe(TimeSpan.Zero);
		success.Value.Hour.ShouldBe(15);
	}

	[Fact]
	void Should_normalize_explicit_offset_to_utc()
	{
		// 15:04:05+05:00 is 10:04:05Z
		DateTimeOffsetParser.ParseRequired("2026-01-02T15:04:05+05:00")
			.TryGetValue(out Success<DateTimeOffset> success).ShouldBeTrue();
		success.Value.Offset.ShouldBe(TimeSpan.Zero);
		success.Value.Hour.ShouldBe(10);
	}

	[Theory]
	[InlineData("2026-01-02T15:04:05")]      // zone-less — ambiguous instant, rejected
	[InlineData("2026-01-02 15:04:05Z")]     // space separator — not ISO
	[InlineData("1/2/2026 3:04 PM")]
	void Should_fail_with_malformed_reason_when_iso_is_unrecognized_or_zoneless(string input)
	{
		var actual = DateTimeOffsetParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateTimeOffset");
		failure.Format.ShouldBe("ISO 8601");
	}

	[Fact]
	void Should_reject_sentinel_instants_as_malformed()
	{
		DateTimeOffsetParser.ParseRequired("0001-01-01T00:00:00Z").TryGetValue(out Failure min).ShouldBeTrue();
		min.Reason.ShouldBe(ParseFailure.Malformed);
		DateTimeOffsetParser.ParseRequired("9999-12-31T23:59:59.9999999Z").TryGetValue(out Failure max).ShouldBeTrue();
		max.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_resolve_zoneless_exact_format_to_utc_never_local()
	{
		DateTimeOffsetParser.ParseExactRequired("2026-01-02 15:04:05", "yyyy-MM-dd HH:mm:ss", _invariant)
			.TryGetValue(out Success<DateTimeOffset> success).ShouldBeTrue();
		success.Value.Offset.ShouldBe(TimeSpan.Zero);
		success.Value.Hour.ShouldBe(15);
	}

	[Theory]
	[InlineData("1700000000", UnixPrecision.Seconds, 2023, 11, 14, 22)]
	[InlineData("1700000000000", UnixPrecision.Milliseconds, 2023, 11, 14, 22)]
	void Should_parse_declared_unix_epoch(string input, UnixPrecision precision, int year, int month, int day, int hour)
	{
		DateTimeOffsetParser.ParseUnix(input, precision)
			.TryGetValue(out Success<DateTimeOffset> success).ShouldBeTrue();
		success.Value.Offset.ShouldBe(TimeSpan.Zero);
		success.Value.Year.ShouldBe(year);
		success.Value.Month.ShouldBe(month);
		success.Value.Day.ShouldBe(day);
		success.Value.Hour.ShouldBe(hour);
	}

	[Fact]
	void Should_parse_negative_unix_epoch_before_1970()
	{
		DateTimeOffsetParser.ParseUnix("-1", UnixPrecision.Seconds)
			.TryGetValue(out Success<DateTimeOffset> success).ShouldBeTrue();
		success.Value.ShouldBe(new(1969, 12, 31, 23, 59, 59, TimeSpan.Zero));
	}

	[Theory]
	[InlineData("1700000000.5")] // fractional epoch is not an integer
	[InlineData("not-a-number")]
	void Should_fail_with_malformed_reason_when_unix_input_is_not_integer(string input)
	{
		DateTimeOffsetParser.ParseUnix(input, UnixPrecision.Seconds)
			.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_not_guess_a_bare_number_as_a_date_on_the_iso_door() =>
		DateTimeOffsetParser.ParseRequired("1700000000").TryGetValue(out Failure _).ShouldBeTrue();

	[Fact]
	void Should_throw_when_unix_precision_is_undefined() =>
		Should.Throw<ArgumentOutOfRangeException>(() => DateTimeOffsetParser.ParseUnix("1700000000", default));

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		DateTimeOffsetParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.ExpectedType.ShouldBe("DateTimeOffset");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		DateTimeOffsetParser.ParseOptional(input).HasValue.ShouldBeFalse();

	[Fact]
	void Should_return_absent_when_optional_unix_input_is_absent() =>
		DateTimeOffsetParser.ParseUnixOptional("   ", UnixPrecision.Seconds).HasValue.ShouldBeFalse();
}
