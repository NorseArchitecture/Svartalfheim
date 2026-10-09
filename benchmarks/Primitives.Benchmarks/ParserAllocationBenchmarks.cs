namespace Norse.Primitives.Benchmarks;

// Success-path allocation sweep over every engine door the gateway routes. The contract under test
// is the Allocated column: Result<T> is the inline zero-boxing union and the engine crossing hands
// back a Verdict<T> by value, so every value-returning door must read 0 B. The two failure probes
// pin the opposite — the Failure span ctor bounds to MaxInputLength and then allocates a string, so
// the Malformed and OutOfRange paths are honestly non-zero by design (truncation knowledge lives in
// Failure).
[MemoryDiagnoser]
public class ParserAllocationBenchmarks
{
	const string IntInput = "1742";
	const string DecimalInput = "1234.5678";
	const string GuidInput = "d9b2d63d-a233-4123-847b-9c8d3e9f1a2b";
	const string CharInput = "U+0041";
	const string DateOnlyIsoInput = "2026-06-17";
	const string TimeOnlyIsoInput = "13:45:30";
	const string DateTimeIsoInput = "2026-06-17T12:30:00Z";
	const string DateTimeOffsetIsoInput = "2026-06-17T12:30:00+00:00";
	const string TimeSpanColonInput = "1.02:03:04";
	const string TimeSpanIsoInput = "P3DT4H30M";
	const string MalformedInput = "not-a-number";
	const string OutOfRangeInput = "256";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Benchmark]
	public Result<int> WholeNumber() =>
		Parser.ParseRequired<int>(IntInput, _invariant);

	[Benchmark]
	public Result<decimal> Real() =>
		Parser.ParseRequired<decimal>(DecimalInput, _invariant);

	[Benchmark]
	public Result<Guid> Uuid() =>
		Parser.ParseRequired<Guid>(GuidInput, _invariant);

	[Benchmark]
	public Result<char> CodePoint() =>
		Parser.ParseRequired<char>(CharInput, _invariant);

	[Benchmark]
	public Result<DateOnly> DateOnlyIso() =>
		Parser.ParseRequired<DateOnly>(DateOnlyIsoInput, _invariant);

	[Benchmark]
	public Result<TimeOnly> TimeOnlyIso() =>
		Parser.ParseRequired<TimeOnly>(TimeOnlyIsoInput, _invariant);

	[Benchmark]
	public Result<DateTime> DateTimeIso() =>
		Parser.ParseRequired<DateTime>(DateTimeIsoInput, _invariant);

	[Benchmark]
	public Result<DateTimeOffset> DateTimeOffsetIso() =>
		Parser.ParseRequired<DateTimeOffset>(DateTimeOffsetIsoInput, _invariant);

	[Benchmark]
	public Result<TimeSpan> TimeSpanColon() =>
		Parser.ParseRequired<TimeSpan>(TimeSpanColonInput, _invariant);

	[Benchmark]
	public Result<TimeSpan> TimeSpanIso() =>
		Parser.ParseRequired<TimeSpan>(TimeSpanIsoInput, _invariant);

	// Failure probes: the Malformed and OutOfRange span ctors truncate and allocate — expected
	// non-zero, the reference points that prove the 0 B success rows above are real.
	[Benchmark]
	public Result<int> MalformedAllocates() =>
		Parser.ParseRequired<int>(MalformedInput, _invariant);

	[Benchmark]
	public Result<byte> OutOfRangeAllocates() =>
		Parser.ParseRequired<byte>(OutOfRangeInput, _invariant);
}
