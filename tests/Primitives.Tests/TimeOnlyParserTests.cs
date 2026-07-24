using System.Globalization;

namespace Norse.Primitives.Tests;

public sealed class TimeOnlyParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Theory]
	[InlineData("15:04:05", 15, 4, 5, 0)]
	[InlineData("15:04:05.123", 15, 4, 5, 123)]
	[InlineData("15:04", 15, 4, 0, 0)]
	void Should_parse_value_when_iso_time_is_recognized(string input, int h, int m, int s, int ms)
	{
		var actual = TimeOnlyParser.ParseRequired(input);
		actual.TryGetValue(out Success<TimeOnly> success).ShouldBeTrue();
		success.Value.ShouldBe(new(h, m, s, ms));
	}

	[Fact]
	void Should_accept_midnight_and_last_tick_as_valid_clock_readings()
	{
		TimeOnlyParser.ParseRequired("00:00:00").TryGetValue(out Success<TimeOnly> midnight).ShouldBeTrue();
		midnight.Value.ShouldBe(TimeOnly.MinValue);
		TimeOnlyParser.ParseRequired("23:59:59.9999999").TryGetValue(out Success<TimeOnly> lastTick).ShouldBeTrue();
		lastTick.Value.ShouldBe(TimeOnly.MaxValue);
	}

	[Theory]
	[InlineData("3:04:05 PM")]   // 12-hour is a declared-format concern, not ISO
	[InlineData("25:00")]
	[InlineData("noon")]
	void Should_fail_with_malformed_reason_when_iso_time_is_unrecognized(string input)
	{
		var actual = TimeOnlyParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("TimeOnly");
		failure.Format.ShouldBe("ISO 8601");
	}

	[Fact]
	void Should_honor_declared_12_hour_format_on_the_exact_door()
	{
		TimeOnlyParser.ParseExactRequired("3:04:05 PM", "h:mm:ss tt", _invariant)
			.TryGetValue(out Success<TimeOnly> success).ShouldBeTrue();
		success.Value.ShouldBe(new(15, 4, 5));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = TimeOnlyParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.ExpectedType.ShouldBe("TimeOnly");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		TimeOnlyParser.ParseOptional(input).HasValue.ShouldBeFalse();

	[Fact]
	void Should_throw_when_exact_format_is_empty() =>
		Should.Throw<ArgumentException>(() => TimeOnlyParser.ParseExactRequired("15:04", "", _invariant));
}
