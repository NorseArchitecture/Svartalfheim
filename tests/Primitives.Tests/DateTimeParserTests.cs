using System.Globalization;

namespace Norse.Primitives.Tests;

public sealed class DateTimeParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Fact]
	void Should_parse_utc_zone_to_utc_kind()
	{
		DateTimeParser.ParseRequired("2026-01-02T15:04:05Z")
			.TryGetValue(out Success<DateTime> success).ShouldBeTrue();
		success.Value.Kind.ShouldBe(DateTimeKind.Utc);
		success.Value.Hour.ShouldBe(15);
	}

	[Fact]
	void Should_normalize_offset_to_utc()
	{
		DateTimeParser.ParseRequired("2026-01-02T15:04:05+05:00")
			.TryGetValue(out Success<DateTime> success).ShouldBeTrue();
		success.Value.Kind.ShouldBe(DateTimeKind.Utc);
		success.Value.Hour.ShouldBe(10);
	}

	[Theory]
	[InlineData("2026-01-02T15:04:05")]   // zone-less rejected
	[InlineData("2026-01-02 15:04:05Z")]  // space separator rejected
	void Should_fail_with_malformed_reason_when_iso_is_zoneless_or_spaced(string input)
	{
		DateTimeParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateTime");
		failure.Format.ShouldBe("ISO 8601");
	}

	[Fact]
	void Should_reject_sentinel_datetimes_as_malformed()
	{
		DateTimeParser.ParseRequired("0001-01-01T00:00:00Z").TryGetValue(out Failure min).ShouldBeTrue();
		min.Reason.ShouldBe(ParseFailure.Malformed);
		DateTimeParser.ParseRequired("9999-12-31T23:59:59.9999999Z").TryGetValue(out Failure max).ShouldBeTrue();
		max.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_parse_declared_unix_epoch_to_utc_datetime()
	{
		DateTimeParser.ParseUnix("1700000000", UnixPrecision.Seconds)
			.TryGetValue(out Success<DateTime> success).ShouldBeTrue();
		success.Value.Kind.ShouldBe(DateTimeKind.Utc);
		success.Value.Year.ShouldBe(2023);
	}

	[Fact]
	void Should_honor_declared_format_on_the_exact_door()
	{
		DateTimeParser.ParseExactRequired("2026-01-02 15:04:05", "yyyy-MM-dd HH:mm:ss", _invariant)
			.TryGetValue(out Success<DateTime> success).ShouldBeTrue();
		success.Value.Hour.ShouldBe(15);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		DateTimeParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.ExpectedType.ShouldBe("DateTime");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		DateTimeParser.ParseOptional(input).HasValue.ShouldBeFalse();

	[Fact]
	void Should_throw_when_unix_precision_is_undefined() =>
		Should.Throw<ArgumentOutOfRangeException>(() => DateTimeParser.ParseUnix("1700000000", default));
}
