using System.Globalization;

namespace Norse.Primitives.Tests;

// Runs in NativeCapabilityCollection: the "_on_the_forced_managed_path" theories/facts below call
// NativeCapability.ForManagedOnly, which mutates thread-local state that must not race another
// test reading NativeCapability.Available concurrently.
[Collection(nameof(NativeCapabilityCollection))]
public sealed class TimeOnlyParserTests
{
	const string AllWhitespace = " \t\r\n\f ";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	public static TheoryData<string, int, int, int, int> RecognizedIsoTimeInputs => new()
	{
		{ "15:04:05", 15, 4, 5, 0 },
		{ "15:04:05.123", 15, 4, 5, 123 },
		{ "15:04", 15, 4, 0, 0 },
	};

	[Theory]
	[MemberData(nameof(RecognizedIsoTimeInputs))]
	void Should_parse_value_when_iso_time_is_recognized(string input, int h, int m, int s, int ms)
	{
		var actual = TimeOnlyParser.ParseRequired(input);
		actual.TryGetValue(out Success<TimeOnly> success).ShouldBeTrue();
		success.Value.ShouldBe(new(h, m, s, ms));
	}

	[Theory]
	[MemberData(nameof(RecognizedIsoTimeInputs))]
	void Should_parse_value_when_iso_time_is_recognized_on_the_forced_managed_path(string input, int h, int m, int s, int ms) =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = TimeOnlyParser.ParseRequired(input);
			actual.TryGetValue(out Success<TimeOnly> success).ShouldBeTrue();
			success.Value.ShouldBe(new(h, m, s, ms));
		});

	[Fact]
	void Should_accept_midnight_and_last_tick_as_valid_clock_readings()
	{
		TimeOnlyParser.ParseRequired("00:00:00").TryGetValue(out Success<TimeOnly> midnight).ShouldBeTrue();
		midnight.Value.ShouldBe(TimeOnly.MinValue);
		TimeOnlyParser.ParseRequired("23:59:59.9999999").TryGetValue(out Success<TimeOnly> lastTick).ShouldBeTrue();
		lastTick.Value.ShouldBe(TimeOnly.MaxValue);
	}

	[Fact]
	void Should_accept_midnight_and_last_tick_as_valid_clock_readings_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			TimeOnlyParser.ParseRequired("00:00:00").TryGetValue(out Success<TimeOnly> midnight).ShouldBeTrue();
			midnight.Value.ShouldBe(TimeOnly.MinValue);
			TimeOnlyParser.ParseRequired("23:59:59.9999999").TryGetValue(out Success<TimeOnly> lastTick).ShouldBeTrue();
			lastTick.Value.ShouldBe(TimeOnly.MaxValue);
		});

	[Fact]
	void Should_truncate_a_nine_digit_fraction_to_tick_precision_rather_than_round()
	{
		// HyperCast's own grammar: one to nine fractional digits; the eighth and ninth truncate
		// (never round) to the tick (100ns) the BCL can actually represent.
		var actual = TimeOnlyParser.ParseRequired("23:59:59.999999999");
		actual.TryGetValue(out Success<TimeOnly> success).ShouldBeTrue();
		success.Value.ShouldBe(TimeOnly.MaxValue);
	}

	[Fact]
	void Should_truncate_a_nine_digit_fraction_to_tick_precision_rather_than_round_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = TimeOnlyParser.ParseRequired("23:59:59.999999999");
			actual.TryGetValue(out Success<TimeOnly> success).ShouldBeTrue();
			success.Value.ShouldBe(TimeOnly.MaxValue);
		});

	[Theory]
	[InlineData("3:04:05 PM")]   // 12-hour is a declared-format concern, not ISO
	[InlineData("25:00")]
	[InlineData("noon")]
	[InlineData("15:60")]              // minute out of range
	[InlineData("15:04:60")]           // second out of range (no leap seconds)
	[InlineData("15:04:05.")]          // trailing dot, zero fraction digits -- not a silent zero
	[InlineData("15:04:05.0000000001")] // ten fractional digits -- no tick-level representation
	[InlineData("15:04.5")]            // fraction without a seconds field
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
