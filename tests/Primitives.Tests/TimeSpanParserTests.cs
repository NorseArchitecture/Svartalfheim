using System.Globalization;

namespace Norse.Primitives.Tests;

// Runs in NativeCapabilityCollection: the "_on_the_forced_managed_path" theories/facts below call
// NativeCapability.ForManagedOnly, which mutates thread-local state that must not race another
// test reading NativeCapability.Available concurrently.
[Collection(nameof(NativeCapabilityCollection))]
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
		colon.Value.ShouldBe(new(1, 30, 0));
		iso.Value.ShouldBe(new(1, 30, 0));
	}

	[Fact]
	void Should_parse_colon_and_iso_to_the_same_span_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired("01:30:00").TryGetValue(out Success<TimeSpan> colon).ShouldBeTrue();
			TimeSpanParser.ParseRequired("PT1H30M").TryGetValue(out Success<TimeSpan> iso).ShouldBeTrue();
			colon.Value.ShouldBe(new(1, 30, 0));
			iso.Value.ShouldBe(new(1, 30, 0));
		});

	[Fact]
	void Should_parse_iso_weeks_designator()
	{
		TimeSpanParser.ParseRequired("P2W").TryGetValue(out Success<TimeSpan> weeks).ShouldBeTrue();
		weeks.Value.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	void Should_parse_iso_weeks_designator_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired("P2W").TryGetValue(out Success<TimeSpan> weeks).ShouldBeTrue();
			weeks.Value.ShouldBe(TimeSpan.FromDays(14));
		});

	[Fact]
	void Should_parse_iso_fractional_seconds()
	{
		TimeSpanParser.ParseRequired("PT1.5S").TryGetValue(out Success<TimeSpan> frac).ShouldBeTrue();
		frac.Value.ShouldBe(TimeSpan.FromSeconds(1.5));
	}

	[Fact]
	void Should_parse_iso_fractional_seconds_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired("PT1.5S").TryGetValue(out Success<TimeSpan> frac).ShouldBeTrue();
			frac.Value.ShouldBe(TimeSpan.FromSeconds(1.5));
		});

	[Fact]
	void Should_parse_negative_iso_duration()
	{
		TimeSpanParser.ParseRequired("-PT1H").TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.ShouldBe(TimeSpan.FromHours(-1));
	}

	[Fact]
	void Should_parse_negative_iso_duration_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired("-PT1H").TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
			success.Value.ShouldBe(TimeSpan.FromHours(-1));
		});

	[Fact]
	void Should_accept_zero_as_valid()
	{
		TimeSpanParser.ParseRequired("00:00:00").TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	void Should_accept_zero_as_valid_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired("00:00:00").TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
			success.Value.ShouldBe(TimeSpan.Zero);
		});

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
		success.Value.ShouldBe(new(1, 30, 0));
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

	// HyperCast's own Duration door reports these as OutOfRange, not Malformed -- both sentinels
	// sit far past the ±10,000-year protobuf Duration ceiling every grammar shares, verified
	// against the vendored corpus (duration.json's "10675199.02:48:05.4775807" vector, the exact
	// TimeSpan.MaxValue round-trip) and against the native binary directly.
	[Theory]
	[InlineData("10675199.02:48:05.4775807")]   // TimeSpan.MaxValue round-trip
	[InlineData("-10675199.02:48:05.4775808")]  // TimeSpan.MinValue round-trip
	void Should_reject_sentinel_spans_as_out_of_range(string input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.OutOfRange);
	}

	// Converged to HyperCast's own verdict (duration.json's matching vectors all expect
	// out_of_range): an 18-digit-or-fewer component that overflows the ±10,000-year cap is
	// OutOfRange, not Malformed -- Malformed is reserved for a component past the MaxDigits
	// sanity bound itself (Should_fail_with_malformed_reason_when_duration_is_unrecognized below
	// covers that separately).
	[Theory]
	[InlineData("PT999999999999999999S")]   // 18-digit seconds, past the ±10,000-year cap
	[InlineData("PT9999999999999999H")]     // 16-digit hours, past the cap
	[InlineData("P9999999999999999W")]      // 16-digit weeks, past the cap
	void Should_fail_with_out_of_range_reason_when_iso_duration_overflows(string input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.OutOfRange);
	}

	// Converged to HyperCast: a colon-form hour is always bounded 0-23 and exactly one or two
	// digits, whether or not a day prefix is present -- the BCL's own "bare hours total"
	// leniency (a wider first component reads as an unbounded hour count) never applied here.
	[Theory]
	[InlineData("25:00:00")]   // hour out of 0-23 range, no day prefix
	[InlineData("100:00:00")]  // hour past the two-digit width, no day prefix
	[InlineData("90")]         // bare digit run: no colon, day prefix, or 's' suffix
	[InlineData("01:60:00")]   // minute out of 0-59 range
	[InlineData("01:30:60")]   // second out of 0-59 range
	[InlineData("+1:30")]      // '+' is never a valid sign, only '-'
	void Should_fail_with_malformed_reason_when_colon_form_grammar_is_violated(string input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	// The short colon form (hh:mm, no seconds) and single-digit hh/mm/ss widths -- confirmed
	// against HyperCast's own corpus and the native binary directly.
	public static TheoryData<string, int, int, int> ShortAndSingleDigitColonForms => new()
	{
		{ "01:30", 1, 30, 0 },
		{ "1:30", 1, 30, 0 },
		{ "1:5:00", 1, 5, 0 },
		{ "01:02:3", 1, 2, 3 },
	};

	[Theory]
	[MemberData(nameof(ShortAndSingleDigitColonForms))]
	void Should_parse_short_and_single_digit_colon_forms(string input, int hours, int minutes, int seconds)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.ShouldBe(new TimeSpan(hours, minutes, seconds));
	}

	[Theory]
	[MemberData(nameof(ShortAndSingleDigitColonForms))]
	void Should_parse_short_and_single_digit_colon_forms_on_the_forced_managed_path(string input, int hours, int minutes, int seconds) =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired(input).TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
			success.Value.ShouldBe(new TimeSpan(hours, minutes, seconds));
		});

	// Protobuf JSON seconds ("3.5s"), case-insensitive on the suffix, with either '.' or ','
	// as the fractional decimal mark -- HyperCast's third duration shape, absent from the
	// pre-Task-14 grammar entirely.
	public static TheoryData<string, int> ProtobufSecondsForm => new()
	{
		{ "5400s", 5400 },
		{ "5400S", 5400 },
		{ "0s", 0 },
	};

	[Theory]
	[MemberData(nameof(ProtobufSecondsForm))]
	void Should_parse_the_protobuf_seconds_form(string input, int expectedSeconds)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
	}

	[Theory]
	[MemberData(nameof(ProtobufSecondsForm))]
	void Should_parse_the_protobuf_seconds_form_on_the_forced_managed_path(string input, int expectedSeconds) =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired(input).TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
			success.Value.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
		});

	[Fact]
	void Should_parse_negative_protobuf_seconds_with_a_fraction()
	{
		TimeSpanParser.ParseRequired("-1.5s").TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.ShouldBe(TimeSpan.FromSeconds(-1.5));
	}

	[Fact]
	void Should_parse_negative_protobuf_seconds_with_a_fraction_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired("-1.5s").TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
			success.Value.ShouldBe(TimeSpan.FromSeconds(-1.5));
		});

	// A comma decimal mark is accepted wherever a period is, on every one of the three grammars.
	public static TheoryData<string> CommaDecimalMarkInputs =>
	[
		"PT1,5S",
		"0:00:01,5",
		"-1,5s",
	];

	[Theory]
	[MemberData(nameof(CommaDecimalMarkInputs))]
	void Should_accept_a_comma_decimal_mark(string input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
		success.Value.Duration().ShouldBe(TimeSpan.FromSeconds(1.5));
	}

	[Theory]
	[MemberData(nameof(CommaDecimalMarkInputs))]
	void Should_accept_a_comma_decimal_mark_on_the_forced_managed_path(string input) =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired(input).TryGetValue(out Success<TimeSpan> success).ShouldBeTrue();
			success.Value.Duration().ShouldBe(TimeSpan.FromSeconds(1.5));
		});

	// A tenth-or-later fractional digit has no tick representation and is unrecognized --
	// mirrors DateTimeOffsetParser's identical rule for its own fractional-second tail.
	[Theory]
	[InlineData("5.1234567890s")]
	[InlineData("PT1.1234567890S")]
	[InlineData("0:00:00.1234567890")]
	void Should_fail_with_malformed_reason_when_fraction_exceeds_nine_digits(string input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	// Regression: every other ISO unit designator ('W'/'w', 'D'/'d', 'H'/'h', 'S'/'s') accepts
	// either case; 'M' alone was case-sensitive even though the `when inTime` guard already
	// disambiguates minutes from months (the `!inTime` months branch is separate) -- case-sensitivity
	// bought nothing. HyperCast's native engine accepts lowercase 'm' for minutes.
	[Fact]
	void Should_parse_lowercase_m_as_minutes_in_the_time_section()
	{
		TimeSpanParser.ParseRequired("PT1m").TryGetValue(out Success<TimeSpan> lower).ShouldBeTrue();
		TimeSpanParser.ParseRequired("PT1M").TryGetValue(out Success<TimeSpan> upper).ShouldBeTrue();
		lower.Value.ShouldBe(upper.Value);
		lower.Value.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	void Should_parse_lowercase_m_as_minutes_in_the_time_section_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeSpanParser.ParseRequired("PT1m").TryGetValue(out Success<TimeSpan> lower).ShouldBeTrue();
			lower.Value.ShouldBe(TimeSpan.FromMinutes(1));
		});

	// The `!inTime` months-rejection path is unaffected by the case-insensitivity fix: lowercase
	// 'm' outside the time section (before 'T') still falls through to the default/months-rejection
	// case, same as uppercase 'M' already does there.
	[Theory]
	[InlineData("P1m")]
	[InlineData("P1M")]
	void Should_still_reject_lowercase_or_uppercase_m_as_months_outside_the_time_section(string input)
	{
		TimeSpanParser.ParseRequired(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}
}
