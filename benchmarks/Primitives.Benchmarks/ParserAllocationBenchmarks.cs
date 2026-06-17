using System.Globalization;
using BenchmarkDotNet.Attributes;

namespace Norse.Primitives.Benchmarks;

// Success-path allocation sweep for the parsers landed in the numeric/char/Guid and temporal
// increments. The contract under test is the Allocated column: Result<T> is the inline
// zero-boxing union, so every value-returning door must read 0 B. The lone failure probe pins
// the opposite — the Failure span ctor bounds to MaxInputLength and then allocates a string, so
// the Malformed path is honestly non-zero by design (truncation knowledge lives in Failure).
[MemoryDiagnoser]
public class ParserAllocationBenchmarks
{
	const string IntInput = "1742";
	const string DecimalInput = "1234.5678";
	const string GuidInput = "d9b2d63d-a233-4123-847b-9c8d3e9f1a2b";
	const string CharInput = "U+0041";

	const string DateOnlyIsoInput = "2026-06-17";
	const string DateOnlyExactInput = "06/17/2026";
	const string DateOnlyExactFormat = "MM/dd/yyyy";

	const string TimeOnlyIsoInput = "13:45:30";
	const string TimeOnlyExactInput = "3:45:30 PM";
	const string TimeOnlyExactFormat = "h:mm:ss tt";

	const string DateTimeIsoInput = "2026-06-17T12:30:00Z";
	const string DateTimeOffsetIsoInput = "2026-06-17T12:30:00+00:00";
	const string DateTimeExactInput = "2026-06-17 12:30:00";
	const string DateTimeExactFormat = "yyyy-MM-dd HH:mm:ss";
	const string UnixSecondsInput = "1750000000";

	const string TimeSpanColonInput = "1.02:03:04";
	const string TimeSpanIsoInput = "P3DT4H30M";
	const string TimeSpanExactInput = "1.02:03:04";
	const string TimeSpanExactFormat = "c";

	const string MalformedInput = "not-a-number";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Benchmark]
	public Result<int> WholeNumber() =>
		IntegerParser.ParseRequired<int>(IntInput, _invariant);

	[Benchmark]
	public Result<decimal> Real() =>
		RealParser.ParseRequired<decimal>(DecimalInput, _invariant);

	[Benchmark]
	public Result<Guid> Uuid() =>
		GuidParser.ParseRequired(GuidInput);

	[Benchmark]
	public Result<char> CodePoint() =>
		CharParser.ParseRequired(CharInput);

	[Benchmark]
	public Result<DateOnly> DateOnlyIso() =>
		DateOnlyParser.ParseRequired(DateOnlyIsoInput);

	[Benchmark]
	public Result<DateOnly> DateOnlyExact() =>
		DateOnlyParser.ParseExactRequired(DateOnlyExactInput, DateOnlyExactFormat, _invariant);

	[Benchmark]
	public Result<TimeOnly> TimeOnlyIso() =>
		TimeOnlyParser.ParseRequired(TimeOnlyIsoInput);

	[Benchmark]
	public Result<TimeOnly> TimeOnlyExact() =>
		TimeOnlyParser.ParseExactRequired(TimeOnlyExactInput, TimeOnlyExactFormat, _invariant);

	[Benchmark]
	public Result<DateTime> DateTimeIso() =>
		DateTimeParser.ParseRequired(DateTimeIsoInput);

	[Benchmark]
	public Result<DateTime> DateTimeExact() =>
		DateTimeParser.ParseExactRequired(DateTimeExactInput, DateTimeExactFormat, _invariant);

	[Benchmark]
	public Result<DateTime> DateTimeUnix() =>
		DateTimeParser.ParseUnix(UnixSecondsInput, UnixPrecision.Seconds);

	[Benchmark]
	public Result<DateTimeOffset> DateTimeOffsetIso() =>
		DateTimeOffsetParser.ParseRequired(DateTimeOffsetIsoInput);

	[Benchmark]
	public Result<DateTimeOffset> DateTimeOffsetExact() =>
		DateTimeOffsetParser.ParseExactRequired(DateTimeExactInput, DateTimeExactFormat, _invariant);

	[Benchmark]
	public Result<DateTimeOffset> DateTimeOffsetUnix() =>
		DateTimeOffsetParser.ParseUnix(UnixSecondsInput, UnixPrecision.Seconds);

	[Benchmark]
	public Result<TimeSpan> TimeSpanColon() =>
		TimeSpanParser.ParseRequired(TimeSpanColonInput);

	[Benchmark]
	public Result<TimeSpan> TimeSpanIso() =>
		TimeSpanParser.ParseRequired(TimeSpanIsoInput);

	[Benchmark]
	public Result<TimeSpan> TimeSpanExact() =>
		TimeSpanParser.ParseExactRequired(TimeSpanExactInput, TimeSpanExactFormat, _invariant);

	// Failure probe: the Malformed span ctor truncates and allocates — expected non-zero, the
	// reference point that proves the 0 B success rows above are real and not a dead benchmark.
	[Benchmark]
	public Result<int> MalformedAllocates() =>
		IntegerParser.ParseRequired<int>(MalformedInput, _invariant);
}
