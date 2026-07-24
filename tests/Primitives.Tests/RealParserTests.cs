using System.Globalization;

namespace Norse.Primitives.Tests;

public sealed class RealParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider
		_invariant = CultureInfo.InvariantCulture,
		_enUs = CultureInfo.GetCultureInfo("en-US"),
		_deDe = CultureInfo.GetCultureInfo("de-DE");

	[Theory]
	[InlineData("1.5", 1.5)]
	[InlineData("  2.25  ", 2.25)]
	[InlineData("-3.5", -3.5)]
	[InlineData("1,234.5", 1234.5)]   // thousands + decimal, invariant
	[InlineData("(2.5)", -2.5)]       // accounting negative
	[InlineData("2.5e3", 2500)]       // scientific
	[InlineData("50%", 0.5)]          // percentage -> divide by 100
	[InlineData("25.5%", 0.255)]
	void Should_parse_value_when_double_input_is_recognized(string input, double expected)
	{
		var actual = RealParser.ParseRequired<double>(input, _invariant);
		actual.TryGetValue(out Success<double> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Fact]
	void Should_parse_currency_when_provider_declares_the_symbol()
	{
		var actual = RealParser.ParseRequired<decimal>("$1,234.50", _enUs);
		actual.TryGetValue(out Success<decimal> success).ShouldBeTrue();
		success.Value.ShouldBe(1234.50m);
	}

	[Fact]
	void Should_honor_declared_decimal_separator_when_provider_is_german()
	{
		var actual = RealParser.ParseRequired<decimal>("1.234,5", _deDe);
		actual.TryGetValue(out Success<decimal> success).ShouldBeTrue();
		success.Value.ShouldBe(1234.5m);
	}

	[Theory]
	[InlineData("NaN")]
	[InlineData("Infinity")]
	[InlineData("-Infinity")]
	void Should_fail_when_double_input_is_non_finite(string input)
	{
		// The forge admits only finite reals — NaN/±Infinity literals are Malformed.
		var actual = RealParser.ParseRequired<double>(input, _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("Double");
	}

	[Fact]
	void Should_fail_when_double_overflows_to_infinity()
	{
		// Overflow produces Infinity, which the finite guard rejects — fail loud, no asymmetry.
		var actual = RealParser.ParseRequired<double>("1e400", _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_parse_decimal_at_its_documented_maximum()
	{
		var actual = RealParser.ParseRequired<decimal>("79228162514264337593543950335", _invariant);
		actual.TryGetValue(out Success<decimal> success).ShouldBeTrue();
		success.Value.ShouldBe(decimal.MaxValue);
	}

	[Fact]
	void Should_fail_with_malformed_reason_when_decimal_exceeds_digit_guard()
	{
		// 30 significant digit characters — beyond any in-range decimal; fail loud, not silent zero.
		var actual = RealParser.ParseRequired<decimal>("123456789012345678901234567890", _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("Decimal");
	}

	[Theory]
	[InlineData("abc")]
	[InlineData("1.2.3")]
	[InlineData("%")]
	void Should_fail_with_malformed_reason_when_double_input_is_unrecognized(string input)
	{
		var actual = RealParser.ParseRequired<double>(input, _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("Double");
		failure.Format.ShouldBeNull();
		failure.Detail.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = RealParser.ParseRequired<double>(input, _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.ExpectedType.ShouldBe("Double");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		RealParser.ParseOptional<double>(input, _invariant).HasValue.ShouldBeFalse();

	[Fact]
	void Should_parse_value_when_optional_input_is_recognized()
	{
		var actual = RealParser.ParseOptional<float>("1.5", _invariant);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<float> success).ShouldBeTrue();
		success.Value.ShouldBe(1.5f);
	}

	[Fact]
	void Should_throw_when_required_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => RealParser.ParseRequired<double>("1.5", null!));

	[Fact]
	void Should_throw_when_optional_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => RealParser.ParseOptional<double>("1.5", null!));
}
