using System.Globalization;
using System.Numerics;

namespace Norse.Primitives.Tests;

/// <summary>
///     The gateway's own law — routing, translation, and the fallthrough — not the engine's grammar,
///     which HyperCast's corpus proves upstream. Each engine expectation here is one corpus vector,
///     named in a comment so a corpus change is traceable to the test it moves.
/// </summary>
public sealed class ParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	// --- routing: one success per door (HyperCast corpus/<file>.json) ---

	[Fact]
	void Should_route_bool_to_the_engine() => // boolean.json "yes"
		Parser.ParseRequired<bool>("yes", _invariant).ShouldBe(new Success<bool>(true));

	[Theory]
	[InlineData("42", 42)] // integer.json i32 "42"
	[InlineData("  7  ", 7)] // integer.json i32 " 7 "
	[InlineData("1,234", 1234)] // integer.json i32 "1,234"
	[InlineData("(1,234)", -1234)] // integer.json i32 "(1,234)"
	[InlineData("0x2A", 42)] // integer.json i32 "0x2A"
	void Should_route_int_to_the_engine(string input, int expected) =>
		Parser.ParseRequired<int>(input, _invariant).ShouldBe(new Success<int>(expected));

	[Theory]
	[InlineData("1,234.5", 1234.5)] // real.json f64 "1,234.5"
	[InlineData("50%", 0.5)] // real.json f64 "50%"
	void Should_route_double_to_the_engine(string input, double expected) =>
		Parser.ParseRequired<double>(input, _invariant).ShouldBe(new Success<double>(expected));

	[Fact]
	void Should_route_decimal_to_the_engine() => // decimal.json "1234.5678"
		Parser.ParseRequired<decimal>("1234.5678", _invariant).ShouldBe(new Success<decimal>(1234.5678m));

	[Theory]
	[InlineData("A", 'A')] // char.json "A"
	[InlineData("65", 'A')] // char.json "65"
	[InlineData("U+0041", 'A')] // char.json "U+0041"
	[InlineData("&#x41;", 'A')] // char.json "&#x41;"
	[InlineData(" ", ' ')] // char.json " " — one character, verbatim, before trimming (engines-cut spec §4.7)
	void Should_route_char_to_the_engine(string input, char expected) =>
		Parser.ParseRequired<char>(input, _invariant).ShouldBe(new Success<char>(expected));

	[Fact]
	void Should_route_guid_to_the_engine() => // uuid.json "urn:uuid:…"
		Parser.ParseRequired<Guid>("urn:uuid:01020304-0506-0708-090a-0b0c0d0e0f10", _invariant)
			.ShouldBe(new Success<Guid>(new("01020304-0506-0708-090a-0b0c0d0e0f10")));

	[Fact]
	void Should_route_date_to_the_engine() => // date.json "2026-01-02"
		Parser.ParseRequired<DateOnly>("2026-01-02", _invariant).ShouldBe(new Success<DateOnly>(new(2026, 1, 2)));

	[Fact]
	void Should_route_time_to_the_engine() => // time.json "15:04:05.123"
		Parser.ParseRequired<TimeOnly>("15:04:05.123", _invariant).ShouldBe(new Success<TimeOnly>(new(15, 4, 5, 123)));

	[Fact]
	void Should_route_timestamp_to_the_engine_normalized_to_utc() => // timestamp.json "+05:00"
		Parser.ParseRequired<DateTimeOffset>("2026-01-02T15:04:05+05:00", _invariant)
			.ShouldBe(new Success<DateTimeOffset>(new(2026, 1, 2, 10, 4, 5, TimeSpan.Zero)));

	[Fact]
	void Should_route_datetime_to_the_engine_as_utc_kind() // timestamp.json "Z"; engines-cut spec §4.7 DateTime row
	{
		Parser.ParseRequired<DateTime>("2026-01-02T15:04:05Z", _invariant).TryGetValue(out Success<DateTime> success)
			.ShouldBeTrue();
		success.Value.ShouldBe(new DateTime(2026, 1, 2, 15, 4, 5, DateTimeKind.Utc));
		success.Value.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	void Should_reject_a_zone_less_datetime() // timestamp.json "2026-01-02T15:04:05" → malformed
	{
		Parser.ParseRequired<DateTimeOffset>("2026-01-02T15:04:05", _invariant).TryGetValue(out Failure failure)
			.ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_route_duration_to_the_engine() => // duration.json "P1DT6H"
		Parser.ParseRequired<TimeSpan>("P1DT6H", _invariant).ShouldBe(new Success<TimeSpan>(new(1, 6, 0, 0)));

	// --- translation: every CastFailure arrives as its ParseFailure, with the forge's diagnostics ---

	[Fact]
	void Should_translate_malformed_with_the_trimmed_input_and_the_clr_type_name()
	{
		Parser.ParseRequired<int>("  bogus  ", _invariant).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.ShouldBe(new(ParseFailure.Malformed, "bogus", "Int32"));
	}

	[Theory]
	[InlineData("256")] // integer.json u8 "256"
	[InlineData("-1")] // integer.json u8 "-1"
	void Should_translate_out_of_range_for_byte(string input)
	{
		Parser.ParseRequired<byte>(input, _invariant).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.OutOfRange);
		failure.ExpectedType.ShouldBe("Byte");
	}

	[Fact]
	void Should_translate_out_of_range_for_a_code_point_past_the_bmp() // char.json "😀": a C# char cannot hold it
	{
		Parser.ParseRequired<char>("U+1F600", _invariant).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.OutOfRange);
	}

	[Theory]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_translate_empty_when_required_input_is_blank(string input)
	{
		Parser.ParseRequired<int>(input, _invariant).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.ShouldBe(new(ParseFailure.Empty, string.Empty, "Int32"));
	}

	[Fact]
	void Should_bound_the_echoed_input_to_max_input_length()
	{
		var input = new string('x', Failure.MaxInputLength + 50);
		Parser.ParseRequired<long>(input, _invariant).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Input.Length.ShouldBe(Failure.MaxInputLength);
	}

	[Fact]
	void Should_leave_format_and_detail_null_from_the_engine()
	{
		Parser.ParseRequired<Guid>("nope", _invariant).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Format.ShouldBeNull();
		failure.Detail.ShouldBeNull();
	}

	// --- optional ---

	[Theory]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_blank(string input) =>
		Parser.ParseOptional<int>(input, _invariant).ShouldBeNull();

	[Fact]
	void Should_return_the_value_when_optional_input_is_present() =>
		Parser.ParseOptional<bool>("no", _invariant).ShouldBe(new Success<bool>(false));

	[Fact]
	void Should_return_the_failure_when_optional_input_is_malformed()
	{
		var actual = Parser.ParseOptional<Guid>("nope", _invariant);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_return_absent_through_the_fallthrough_when_optional_input_is_blank() =>
		Parser.ParseOptional<string>(AllWhitespace, _invariant).ShouldBeNull();

	// --- culture: the provider passes straight through to the engine's bridge (engines-cut spec §4.4) ---

	[Fact]
	void Should_honor_a_declared_culture_for_numeric_doors() =>
		Parser.ParseRequired<decimal>("1.234,5", CultureInfo.GetCultureInfo("de-DE")).ShouldBe(new Success<decimal>(1234.5m));

	[Fact]
	void Should_ignore_the_provider_on_culture_insensitive_doors() =>
		Parser.ParseRequired<DateOnly>("2026-01-02", CultureInfo.GetCultureInfo("de-DE"))
			.ShouldBe(new Success<DateOnly>(new(2026, 1, 2)));

	[Fact]
	void Should_throw_when_provider_is_null() =>
		// The one place `null!` is the point: the gateway's provider law is being proven, not dodged.
		Should.Throw<ArgumentNullException>(() => Parser.ParseRequired<int>("1", null!));

	// --- the fallthrough: no engine door, ISpanParsable<T> as before ---

	[Fact]
	void Should_fall_through_to_span_parsable_for_string() =>
		Parser.ParseRequired<string>("  text  ", _invariant).ShouldBe(new Success<string>("text"));

	[Fact]
	void Should_fall_through_to_span_parsable_for_int128() =>
		Parser.ParseRequired<Int128>("170141183460469231731687303715884105727", _invariant)
			.ShouldBe(new Success<Int128>(Int128.MaxValue));

	[Fact]
	void Should_fall_through_to_span_parsable_for_big_integer() =>
		Parser.ParseRequired<BigInteger>("12345678901234567890123", _invariant)
			.ShouldBe(new Success<BigInteger>(BigInteger.Parse("12345678901234567890123", CultureInfo.InvariantCulture)));

	[Fact]
	void Should_report_malformed_through_the_fallthrough()
	{
		Parser.ParseRequired<Half>("nope", _invariant).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.ShouldBe(new(ParseFailure.Malformed, "nope", "Half"));
	}

	[Fact]
	void Should_not_leak_boolean_vocabulary_into_int()
	{
		Parser.ParseRequired<int>("yes", _invariant).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}
}
