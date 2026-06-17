using System.Globalization;

namespace Norse.Primitives.Tests;

public sealed class ParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Theory]
	[InlineData("yes")]
	[InlineData("on")]
	[InlineData("1")]
	void Should_route_to_boolean_specialist_when_parsing_bool(string input)
	{
		var actual = Parser.ParseRequired<bool>(input, _invariant);
		actual.TryGetValue(out Success<bool> success).ShouldBeTrue();
		success.Value.ShouldBeTrue();
	}

	[Fact]
	void Should_route_to_boolean_specialist_when_parsing_optional_bool()
	{
		var actual = Parser.ParseOptional<bool>("no", _invariant);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<bool> success).ShouldBeTrue();
		success.Value.ShouldBeFalse();
	}

	[Fact]
	void Should_route_failure_through_boolean_specialist_when_bool_input_is_unrecognized()
	{
		var actual = Parser.ParseRequired<bool>("maybe", _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("Boolean");
	}

	[Fact]
	void Should_return_absent_when_optional_bool_input_is_absent() =>
		Parser.ParseOptional<bool>("  ", _invariant).HasValue.ShouldBeFalse();

	[Fact]
	void Should_not_leak_boolean_vocabulary_when_parsing_int()
	{
		var actual = Parser.ParseRequired<int>("yes", _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("Int32");
	}

	[Theory]
	[InlineData("42", 42)]
	[InlineData("  7  ", 7)]
	[InlineData("-13", -13)]
	void Should_parse_value_when_int_input_is_recognized(string input, int expected)
	{
		var actual = Parser.ParseRequired<int>(input, _invariant);
		actual.TryGetValue(out Success<int> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Fact]
	void Should_honor_declared_provider_when_parsing_decimal()
	{
		Parser.ParseRequired<decimal>("1.5", _invariant)
			.TryGetValue(out Success<decimal> invariantSuccess).ShouldBeTrue();
		invariantSuccess.Value.ShouldBe(1.5m);
		Parser.ParseRequired<decimal>("1,5", CultureInfo.GetCultureInfo("de-DE"))
			.TryGetValue(out Success<decimal> germanSuccess).ShouldBeTrue();
		germanSuccess.Value.ShouldBe(1.5m);
	}

	[Fact]
	void Should_parse_value_when_guid_rides_the_generic_path()
	{
		var expected = Guid.NewGuid();
		var actual = Parser.ParseRequired<Guid>(expected.ToString("D"), _invariant);
		actual.TryGetValue(out Success<Guid> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = Parser.ParseRequired<int>(input, _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.Input.ShouldBe(string.Empty);
		failure.ExpectedType.ShouldBe("Int32");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		Parser.ParseOptional<int>(input, _invariant).HasValue.ShouldBeFalse();

	[Theory]
	[InlineData("abc")]
	[InlineData("12.5")]
	[InlineData("fourty-two")]
	void Should_fail_with_malformed_reason_when_int_input_is_unrecognized(string input)
	{
		var actual = Parser.ParseRequired<int>(input, _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.Input.ShouldBe(input.Trim());
		failure.ExpectedType.ShouldBe("Int32");
		failure.Format.ShouldBeNull();
		failure.Detail.ShouldBeNull();
	}

	[Fact]
	void Should_truncate_captured_input_when_malformed_input_is_oversized()
	{
		var oversized = new string('9', Failure.MaxInputLength + 44);
		var actual = Parser.ParseRequired<int>(oversized, _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.Input.Length.ShouldBe(Failure.MaxInputLength);
	}

	[Fact]
	void Should_fail_with_malformed_reason_when_optional_input_is_unrecognized()
	{
		var actual = Parser.ParseOptional<int>("abc", _invariant);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_throw_when_required_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => Parser.ParseRequired<int>("42", null!));

	[Fact]
	void Should_throw_when_optional_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => Parser.ParseOptional<int>("42", null!));

	[Fact]
	void Should_route_integer_vocabulary_through_the_gateway()
	{
		Parser.ParseRequired<int>("1,234", _invariant)
			.TryGetValue(out Success<int> thousands).ShouldBeTrue();
		thousands.Value.ShouldBe(1234);
		Parser.ParseRequired<int>("0x2A", _invariant)
			.TryGetValue(out Success<int> hex).ShouldBeTrue();
		hex.Value.ShouldBe(42);
	}

	[Fact]
	void Should_route_real_percentage_through_the_gateway()
	{
		Parser.ParseRequired<double>("50%", _invariant)
			.TryGetValue(out Success<double> success).ShouldBeTrue();
		success.Value.ShouldBe(0.5);
	}

	[Fact]
	void Should_route_char_code_point_through_the_gateway()
	{
		Parser.ParseRequired<char>("65", _invariant)
			.TryGetValue(out Success<char> success).ShouldBeTrue();
		success.Value.ShouldBe('A');
	}

	[Fact]
	void Should_route_guid_prefix_through_the_gateway()
	{
		var expected = new Guid("01020304-0506-0708-090a-0b0c0d0e0f10");
		Parser.ParseRequired<Guid>("urn:uuid:01020304-0506-0708-090a-0b0c0d0e0f10", _invariant)
			.TryGetValue(out Success<Guid> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Fact]
	void Should_route_optional_integer_vocabulary_through_the_gateway()
	{
		var actual = Parser.ParseOptional<int>("(7)", _invariant);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<int> success).ShouldBeTrue();
		success.Value.ShouldBe(-7);
	}

	[Fact]
	void Should_require_provider_even_for_culture_insensitive_char() =>
		Should.Throw<ArgumentNullException>(() => Parser.ParseRequired<char>("A", null!));

	[Fact]
	void Should_route_iso_date_through_the_gateway()
	{
		Parser.ParseRequired<DateOnly>("2026-01-02", _invariant)
			.TryGetValue(out Success<DateOnly> success).ShouldBeTrue();
		success.Value.ShouldBe(new DateOnly(2026, 1, 2));
	}

	[Fact]
	void Should_route_iso_datetimeoffset_to_utc_through_the_gateway()
	{
		Parser.ParseRequired<DateTimeOffset>("2026-01-02T15:04:05+05:00", _invariant)
			.TryGetValue(out Success<DateTimeOffset> success).ShouldBeTrue();
		success.Value.Offset.ShouldBe(TimeSpan.Zero);
		success.Value.Hour.ShouldBe(10);
	}

	[Fact]
	void Should_route_iso_datetime_and_time_and_timespan_through_the_gateway()
	{
		Parser.ParseRequired<DateTime>("2026-01-02T15:04:05Z", _invariant)
			.TryGetValue(out Success<DateTime> dateTime).ShouldBeTrue();
		dateTime.Value.Kind.ShouldBe(DateTimeKind.Utc);
		Parser.ParseRequired<TimeOnly>("15:04:05", _invariant)
			.TryGetValue(out Success<TimeOnly> time).ShouldBeTrue();
		time.Value.ShouldBe(new TimeOnly(15, 4, 5));
		Parser.ParseRequired<TimeSpan>("PT1H30M", _invariant)
			.TryGetValue(out Success<TimeSpan> span).ShouldBeTrue();
		span.Value.ShouldBe(new TimeSpan(1, 30, 0));
	}

	[Theory]
	[InlineData("1/2/2026")]              // US slash date
	[InlineData("2026-01-02T15:04:05")]   // zone-less datetime
	void Should_reject_non_iso_temporal_through_the_gateway(string input)
	{
		Parser.ParseRequired<DateTimeOffset>(input, _invariant)
			.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.Format.ShouldBe("ISO 8601");
	}

	[Fact]
	void Should_not_guess_a_bare_number_as_a_date_through_the_gateway() =>
		Parser.ParseRequired<DateTimeOffset>("1700000000", _invariant)
			.TryGetValue(out Failure _).ShouldBeTrue();

	[Fact]
	void Should_route_optional_temporal_absence_as_null_through_the_gateway() =>
		Parser.ParseOptional<DateOnly>("  ", _invariant).HasValue.ShouldBeFalse();

	[Fact]
	void Should_require_provider_even_for_culture_insensitive_temporal() =>
		Should.Throw<ArgumentNullException>(() => Parser.ParseRequired<DateOnly>("2026-01-02", null!));
}
