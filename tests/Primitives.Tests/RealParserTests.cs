using System.Globalization;

namespace Norse.Primitives.Tests;

// Runs in NativeCapabilityCollection: the "_on_the_forced_managed_path" theories/facts below call
// NativeCapability.ForManagedOnly, which mutates thread-local state that must not race another
// test reading NativeCapability.Available concurrently.
[Collection(nameof(NativeCapabilityCollection))]
public sealed class RealParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider
		_invariant = CultureInfo.InvariantCulture,
		_enUs = CultureInfo.GetCultureInfo("en-US"),
		_deDe = CultureInfo.GetCultureInfo("de-DE");

	public static TheoryData<string, double> RecognizedDoubleInputs => new()
	{
		{ "1.5", 1.5 },
		{ "  2.25  ", 2.25 },
		{ "-3.5", -3.5 },
		{ "1,234.5", 1234.5 },   // thousands + decimal, invariant
		{ "(2.5)", -2.5 },       // accounting negative
		{ "2.5e3", 2500 },       // scientific
		{ "50%", 0.5 },          // percentage -> divide by 100
		{ "25.5%", 0.255 },
	};

	[Theory]
	[MemberData(nameof(RecognizedDoubleInputs))]
	void Should_parse_value_when_double_input_is_recognized(string input, double expected)
	{
		var actual = RealParser.ParseRequired<double>(input, _invariant);
		actual.TryGetValue(out Success<double> success).ShouldBeTrue();
		success.Value.ShouldBe(expected);
	}

	[Theory]
	[MemberData(nameof(RecognizedDoubleInputs))]
	void Should_parse_value_when_double_input_is_recognized_on_the_forced_managed_path(string input, double expected) =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = RealParser.ParseRequired<double>(input, _invariant);
			actual.TryGetValue(out Success<double> success).ShouldBeTrue();
			success.Value.ShouldBe(expected);
		});

	[Fact]
	void Should_parse_currency_when_provider_declares_the_symbol()
	{
		var actual = RealParser.ParseRequired<decimal>("$1,234.50", _enUs);
		actual.TryGetValue(out Success<decimal> success).ShouldBeTrue();
		success.Value.ShouldBe(1234.50m);
	}

	[Fact]
	void Should_parse_currency_symbol_for_double_when_provider_is_culture_aware()
	{
		// Regression: the plain (non-detect) native branch used to route to HyperCast.Cast.Double
		// for ANY CultureInfo provider, including one whose input carries a currency symbol.
		// HyperCast.NumFormat has no currency-symbol concept at all, so native faulted this input
		// as Malformed even though double (unlike decimal, which never native-routes) genuinely
		// supports currency-symbol input via the managed T.TryParse(RealStyles, provider, ...)
		// path. Gating the native branch behind IsInvariant(provider) -- IntegerParser's existing
		// pattern -- routes this to the managed path instead, where it succeeds.
		var actual = RealParser.ParseRequired<double>("$1,234.56", _enUs);
		actual.TryGetValue(out Success<double> success).ShouldBeTrue();
		success.Value.ShouldBe(1234.56);
	}

	[Fact]
	void Should_parse_grouped_double_when_provider_is_invariant()
	{
		// The invariant-culture case is the one the native gate must keep routing natively (when
		// HyperCast is available) -- confirms the IsInvariant(provider) gate added alongside the
		// currency-symbol fix above does not regress the normal, culture-insensitive case.
		var actual = RealParser.ParseRequired<double>("1,234.56", _invariant);
		actual.TryGetValue(out Success<double> success).ShouldBeTrue();
		success.Value.ShouldBe(1234.56);
	}

	[Fact]
	void Should_parse_grouped_double_when_provider_is_invariant_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = RealParser.ParseRequired<double>("1,234.56", _invariant);
			actual.TryGetValue(out Success<double> success).ShouldBeTrue();
			success.Value.ShouldBe(1234.56);
		});

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
	void Should_fail_with_out_of_range_reason_when_double_overflows_to_infinity()
	{
		// A well-formed literal whose magnitude simply exceeds double's finite range is
		// OutOfRange, not Malformed — the same well-formed-but-out-of-range distinction
		// IntegerParser draws, and the same distinction HyperCast's own Cast.Double draws.
		var actual = RealParser.ParseRequired<double>("1e400", _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.OutOfRange);
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

	[Fact]
	void Should_fail_with_out_of_range_reason_when_decimal_overflows()
	{
		// Regression: decimal.MaxValue + 1, 29 significant digits -- numerically well-formed (under
		// the 29-digit DecimalDigitGuard trip point) but its magnitude exceeds decimal's finite
		// range. decimal.TryParse simply returns false on overflow (no infinity concept the way
		// double/float have), so this used to collapse to a bare Malformed instead of the class's
		// own documented OutOfRange contract.
		var actual = RealParser.ParseRequired<decimal>("79228162514264337593543950336", _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.OutOfRange);
		failure.ExpectedType.ShouldBe("Decimal");
	}

	[Fact]
	void Should_fail_with_out_of_range_reason_when_decimal_overflows_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = RealParser.ParseRequired<decimal>("79228162514264337593543950336", _invariant);
			actual.TryGetValue(out Failure failure).ShouldBeTrue();
			failure.Reason.ShouldBe(ParseFailure.OutOfRange);
		});

	[Fact]
	void Should_fail_with_malformed_reason_when_decimal_input_is_not_numeric()
	{
		// A genuinely non-numeric token stays Malformed -- ClassifyOverflow's double-probe also
		// fails to parse it, so it falls through to the Malformed branch, not OutOfRange.
		var actual = RealParser.ParseRequired<decimal>("not-a-number", _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_fail_with_out_of_range_reason_when_decimal_percentage_overflows()
	{
		// The percent branch (trailing '%') funnels through the same ClassifyOverflow helper. The
		// body alone (decimal.MaxValue + 1, still 29 digit characters -- under the digit guard's
		// own trip point) overflows decimal.TryParse regardless of the eventual /100 division.
		var actual = RealParser.ParseRequired<decimal>("79228162514264337593543950336%", _invariant);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.OutOfRange);
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
	void Should_parse_value_when_optional_input_is_recognized_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = RealParser.ParseOptional<float>("1.5", _invariant);
			actual.HasValue.ShouldBeTrue();
			actual.Value.TryGetValue(out Success<float> success).ShouldBeTrue();
			success.Value.ShouldBe(1.5f);
		});

	[Fact]
	void Should_throw_when_required_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => RealParser.ParseRequired<double>("1.5", null!));

	[Fact]
	void Should_throw_when_optional_provider_is_null() =>
		Should.Throw<ArgumentNullException>(() => RealParser.ParseOptional<double>("1.5", null!));

	[Fact]
	void Should_fail_with_malformed_reason_rather_than_throw_when_decimal_input_is_entirely_repeated_separator()
	{
		// Regression: ParseDetected's normalization strips every occurrence of a repeated separator
		// character wholesale (a grouping-only separator is deleted, not converted). Input that is
		// ENTIRELY the repeated separator normalizes to an empty span, which used to be passed back
		// into Parse<T> and throw IndexOutOfRangeException at trimmed[^1] -- decimal never routes
		// native (TryParseNative only handles double/float), so this is the only path for it, on
		// every platform.
		var actual = RealParser.ParseRequired<decimal>("..", CultureInfo.InvariantCulture, detectSeparators: true);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_fail_with_malformed_reason_rather_than_throw_when_managed_double_input_is_entirely_repeated_separator()
	{
		// Same bug, forced onto the managed fallback path for double -- the native short-circuit
		// (ParseDetectedNative) intercepts first on a native-capable host, so the managed-only
		// fallback must still be exercised explicitly to prove it doesn't throw.
		Failure failure = default;
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = RealParser.ParseRequired<double>(",,,", CultureInfo.InvariantCulture, detectSeparators: true);
			actual.TryGetValue(out failure).ShouldBeTrue();
		});
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_fail_with_malformed_reason_rather_than_throw_when_managed_float_input_is_entirely_repeated_separator()
	{
		Failure failure = default;
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = RealParser.ParseRequired<float>("...", CultureInfo.InvariantCulture, detectSeparators: true);
			actual.TryGetValue(out failure).ShouldBeTrue();
		});
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}
}
