using System.Globalization;

namespace Norse.Primitives.Tests;

public sealed class DateOnlyParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider
		_enUs = CultureInfo.GetCultureInfo("en-US"),
		_enGb = CultureInfo.GetCultureInfo("en-GB");

	[Theory]
	[InlineData("2026-01-02")]
	[InlineData("  2026-01-02  ")]
	void Should_parse_value_when_iso_date_is_recognized(string input)
	{
		var actual = DateOnlyParser.ParseRequired(input);
		actual.TryGetValue(out Success<DateOnly> success).ShouldBeTrue();
		success.Value.ShouldBe(new(2026, 1, 2));
	}

	[Theory]
	[InlineData("1/2/2026")]          // US slash — not ISO
	[InlineData("2026-01-02T00:00:00")] // time-bearing — never truncated to the date
	[InlineData("2026/01/02")]
	[InlineData("garbage")]
	void Should_fail_with_malformed_reason_when_iso_date_is_unrecognized(string input)
	{
		var actual = DateOnlyParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateOnly");
		failure.Format.ShouldBe("ISO 8601");
	}

	[Fact]
	void Should_reject_sentinel_dates_as_malformed()
	{
		DateOnlyParser.ParseRequired("0001-01-01").TryGetValue(out Failure min).ShouldBeTrue();
		min.Reason.ShouldBe(ParseFailure.Malformed);
		DateOnlyParser.ParseRequired("9999-12-31").TryGetValue(out Failure max).ShouldBeTrue();
		max.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_honor_declared_format_and_provider_on_the_exact_door()
	{
		DateOnlyParser.ParseExactRequired("1/2/2026", "M/d/yyyy", _enUs)
			.TryGetValue(out Success<DateOnly> us).ShouldBeTrue();
		us.Value.ShouldBe(new(2026, 1, 2));
		DateOnlyParser.ParseExactRequired("1/2/2026", "d/M/yyyy", _enGb)
			.TryGetValue(out Success<DateOnly> gb).ShouldBeTrue();
		gb.Value.ShouldBe(new(2026, 2, 1));
	}

	[Fact]
	void Should_set_format_to_declared_format_when_exact_input_is_malformed()
	{
		DateOnlyParser.ParseExactRequired("nope", "M/d/yyyy", _enUs)
			.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.Format.ShouldBe("M/d/yyyy");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = DateOnlyParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.Input.ShouldBe(string.Empty);
		failure.ExpectedType.ShouldBe("DateOnly");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		DateOnlyParser.ParseOptional(input).HasValue.ShouldBeFalse();

	[Fact]
	void Should_parse_value_when_optional_iso_input_is_recognized()
	{
		var actual = DateOnlyParser.ParseOptional("2026-01-02");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<DateOnly> success).ShouldBeTrue();
		success.Value.ShouldBe(new(2026, 1, 2));
	}

	[Fact]
	void Should_throw_when_exact_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => DateOnlyParser.ParseExactRequired("2026-01-02", "yyyy-MM-dd", null!));

	[Fact]
	void Should_throw_when_exact_format_is_empty() =>
		Should.Throw<ArgumentException>(() => DateOnlyParser.ParseExactRequired("2026-01-02", "", _enUs));
}
