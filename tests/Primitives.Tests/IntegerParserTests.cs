using System.Globalization;

namespace Norse.Primitives.Tests;

public sealed class IntegerParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider
		_invariant = CultureInfo.InvariantCulture,
		_enUs = CultureInfo.GetCultureInfo("en-US"),
		_deDe = CultureInfo.GetCultureInfo("de-DE");

	[Theory]
	[InlineData("42", 42)]
	[InlineData("  7  ", 7)]
	[InlineData("+13", 13)]
	[InlineData("-13", -13)]
	[InlineData("1,234", 1234)]      // thousands, invariant group separator
	[InlineData("(1,234)", -1234)]   // accounting negative
	[InlineData("1e3", 1000)]        // integral exponent
	[InlineData("0x2A", 42)]         // hex prefix
	[InlineData("&H2A", 42)]         // legacy hex prefix
	[InlineData("0b1010", 10)]       // binary prefix
	void Should_parse_value_when_int_input_is_recognized(string input, int expected)
	{
		var actual = IntegerParser.ParseRequired<int>(input, _invariant);
		actual.TryGetValue(out Success<int> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Fact]
	void Should_parse_currency_when_provider_declares_the_symbol()
	{
		var actual = IntegerParser.ParseRequired<int>("$1,234", _enUs);
		actual.TryGetValue(out Success<int> success).ShouldBeTrue();
		success.Value.ShouldBe(1234);
	}

	[Fact]
	void Should_honor_declared_grouping_when_provider_is_german()
	{
		var actual = IntegerParser.ParseRequired<int>("1.234", _deDe);
		actual.TryGetValue(out Success<int> success).ShouldBeTrue();
		success.Value.ShouldBe(1234);
	}

	[Fact]
	void Should_read_hex_as_bit_pattern_when_value_overflows_signed_width()
	{
		// 0xFF is the two's-complement bit pattern -1 for sbyte.
		var actual = IntegerParser.ParseRequired<sbyte>("0xFF", _invariant);
		actual.TryGetValue(out Success<sbyte> success).ShouldBeTrue();
		success.Value.ShouldBe((sbyte)-1);
	}

	[Theory]
	[InlineData("12.5")]     // decimal point never allowed on an integer
	[InlineData("1.5e0")]    // non-integral exponent result
	[InlineData("abc")]
	[InlineData("-0x1F")]    // signed hex is not recognized
	void Should_fail_with_malformed_reason_when_int_input_is_unrecognized(string input)
	{
		var actual = IntegerParser.ParseRequired<int>(input, _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.Input.ShouldBe(input.Trim());
		failure.ExpectedType.ShouldBe("Int32");
		failure.Format.ShouldBeNull();
		failure.Detail.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = IntegerParser.ParseRequired<int>(input, _invariant);
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
		IntegerParser.ParseOptional<int>(input, _invariant).HasValue.ShouldBeFalse();

	[Fact]
	void Should_parse_value_when_optional_input_is_recognized()
	{
		var actual = IntegerParser.ParseOptional<int>("0x2A", _invariant);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<int> success).ShouldBeTrue();
		success.Value.ShouldBe(42);
	}

	[Fact]
	void Should_truncate_captured_input_when_malformed_input_is_oversized()
	{
		var oversized = $"z{new string('9', Failure.MaxInputLength + 44)}";
		var actual = IntegerParser.ParseRequired<int>(oversized, _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Input.Length.ShouldBe(Failure.MaxInputLength);
	}

	[Fact]
	void Should_throw_when_required_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => IntegerParser.ParseRequired<int>("42", null!));

	[Fact]
	void Should_throw_when_optional_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => IntegerParser.ParseOptional<int>("42", null!));

	[Theory]
	[InlineData("0")]
	[InlineData("255")]
	void Should_parse_byte_within_range(string input)
	{
		IntegerParser.ParseRequired<byte>(input, _invariant)
			.TryGetValue(out Success<byte> success).ShouldBeTrue();
		success.Value.ShouldBe(byte.Parse(input, CultureInfo.InvariantCulture));
	}

	[Theory]
	[InlineData("256")]
	[InlineData("-1")]
	void Should_fail_when_byte_is_out_of_range(string input) =>
		IntegerParser.ParseRequired<byte>(input, _invariant)
			.TryGetValue(out Failure _).ShouldBeTrue();

	[Fact]
	void Should_parse_each_integer_width_at_its_documented_maximum()
	{
		IntegerParser.ParseRequired<sbyte>("127", _invariant).TryGetValue(out Success<sbyte> a).ShouldBeTrue();
		a.Value.ShouldBe(sbyte.MaxValue);
		IntegerParser.ParseRequired<short>("32767", _invariant).TryGetValue(out Success<short> b).ShouldBeTrue();
		b.Value.ShouldBe(short.MaxValue);
		IntegerParser.ParseRequired<ushort>("65535", _invariant).TryGetValue(out Success<ushort> c).ShouldBeTrue();
		c.Value.ShouldBe(ushort.MaxValue);
		IntegerParser.ParseRequired<uint>("4294967295", _invariant).TryGetValue(out Success<uint> d).ShouldBeTrue();
		d.Value.ShouldBe(uint.MaxValue);
		IntegerParser.ParseRequired<long>("9223372036854775807", _invariant).TryGetValue(out Success<long> e).ShouldBeTrue();
		e.Value.ShouldBe(long.MaxValue);
		IntegerParser.ParseRequired<ulong>("18446744073709551615", _invariant).TryGetValue(out Success<ulong> f).ShouldBeTrue();
		f.Value.ShouldBe(ulong.MaxValue);
	}

	[Fact]
	void Should_fail_when_value_overflows_long() =>
		IntegerParser.ParseRequired<long>("99999999999999999999999", _invariant)
			.TryGetValue(out Failure _).ShouldBeTrue();
}
