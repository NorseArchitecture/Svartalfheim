using System.Globalization;

namespace Norse.Primitives.Tests;

// Runs in NativeCapabilityCollection: the "_on_the_forced_managed_path" theories/facts below call
// NativeCapability.ForManagedOnly, which mutates thread-local state that must not race another
// test reading NativeCapability.Available concurrently.
[Collection(nameof(NativeCapabilityCollection))]
public sealed class DateOnlyParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider
		_enUs = CultureInfo.GetCultureInfo("en-US"),
		_enGb = CultureInfo.GetCultureInfo("en-GB");

	public static TheoryData<string> RecognizedIsoDateInputs =>
	[
		"2026-01-02",
		"  2026-01-02  ",
	];

	[Theory]
	[MemberData(nameof(RecognizedIsoDateInputs))]
	void Should_parse_value_when_iso_date_is_recognized(string input)
	{
		var actual = DateOnlyParser.ParseRequired(input);
		actual.TryGetValue(out Success<DateOnly> success).ShouldBeTrue();
		success.Value.ShouldBe(new(2026, 1, 2));
	}

	[Theory]
	[MemberData(nameof(RecognizedIsoDateInputs))]
	void Should_parse_value_when_iso_date_is_recognized_on_the_forced_managed_path(string input) =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = DateOnlyParser.ParseRequired(input);
			actual.TryGetValue(out Success<DateOnly> success).ShouldBeTrue();
			success.Value.ShouldBe(new(2026, 1, 2));
		});

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
	void Should_parse_the_representable_boundary_dates_as_ordinary_successes()
	{
		// HyperCast's own corpus (date.json) requires DateOnly.MinValue/MaxValue's ISO text to
		// succeed -- converged per the 2026-09-03 Task 15 amendment to the temporal-parsers spec
		// §9, the same treatment Task 13 already gave DateTimeOffset's ISO door.
		DateOnlyParser.ParseRequired("0001-01-01").TryGetValue(out Success<DateOnly> min).ShouldBeTrue();
		min.Value.ShouldBe(DateOnly.MinValue);
		DateOnlyParser.ParseRequired("9999-12-31").TryGetValue(out Success<DateOnly> max).ShouldBeTrue();
		max.Value.ShouldBe(DateOnly.MaxValue);
	}

	[Fact]
	void Should_parse_the_representable_boundary_dates_as_ordinary_successes_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			DateOnlyParser.ParseRequired("0001-01-01").TryGetValue(out Success<DateOnly> min).ShouldBeTrue();
			min.Value.ShouldBe(DateOnly.MinValue);
			DateOnlyParser.ParseRequired("9999-12-31").TryGetValue(out Success<DateOnly> max).ShouldBeTrue();
			max.Value.ShouldBe(DateOnly.MaxValue);
		});

	[Fact]
	void Should_fail_with_out_of_range_reason_for_a_well_formed_but_unrepresentable_year()
	{
		// "0000" is a well-formed four-digit year token that the proleptic Gregorian calendar
		// cannot represent -- HyperCast's corpus (and this door's native translation) distinguish
		// this from an ordinarily unrecognized string.
		var actual = DateOnlyParser.ParseRequired("0000-01-01");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.OutOfRange);
		failure.ExpectedType.ShouldBe("DateOnly");
	}

	[Fact]
	void Should_still_reject_a_wrong_separator_leading_zero_year_as_malformed()
	{
		// "0000/01/01" carries the same leading-zero year but the wrong separator -- must stay a
		// grammar failure, not be swept into OutOfRange by a naive "starts with 0000" check.
		var actual = DateOnlyParser.ParseRequired("0000/01/01");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_reject_a_leading_zero_year_with_a_non_digit_month_as_malformed()
	{
		// "0000-ab-01" has the right shape (length, dashes at the yyyy-MM-dd positions, literal
		// "0000" year) but the month span is not actually digits -- a naive "starts with 0000,
		// dashes in the right place" check would wrongly promote this to OutOfRange; only a
		// genuinely well-formed "0000-MM-dd" token (month/day both ASCII digits) earns that
		// verdict. Confirmed against HyperCast.Cast.Date directly: real native behavior for this
		// exact input is Fault(Malformed @ 5+2), matching the managed door's own reasoning.
		var actual = DateOnlyParser.ParseRequired("0000-ab-01");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
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
	void Should_parse_value_when_optional_iso_input_is_recognized_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = DateOnlyParser.ParseOptional("2026-01-02");
			actual.HasValue.ShouldBeTrue();
			actual.Value.TryGetValue(out Success<DateOnly> success).ShouldBeTrue();
			success.Value.ShouldBe(new(2026, 1, 2));
		});

	[Fact]
	void Should_throw_when_exact_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => DateOnlyParser.ParseExactRequired("2026-01-02", "yyyy-MM-dd", null!));

	[Fact]
	void Should_throw_when_exact_format_is_empty() =>
		Should.Throw<ArgumentException>(() => DateOnlyParser.ParseExactRequired("2026-01-02", "", _enUs));
}
