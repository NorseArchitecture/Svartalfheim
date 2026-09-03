using HyperCast;

namespace Norse.Primitives.Benchmarks;

// HyperCast parser benchmarks -- proves the native path (wired behind NativeCapability in
// src/Primitives) stays faster than the managed fallback. Permanent fixture: run before/after
// any change to either engine's parsers.
[MemoryDiagnoser]
public class HyperCastBenchmarks
{
	const string BoolInput = "yes";
	const string IntInput = "1,234";
	const string GuidInput = "550e8400-e29b-41d4-a716-446655440000";
	const string TimestampInput = "2026-01-02T15:04:05Z";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Benchmark(Baseline = true)]
	public Result<bool> BooleanSvartalfheim() =>
		BooleanParser.ParseRequired(BoolInput);

	[Benchmark]
	public Verdict<bool> BooleanHyperCast() =>
		Cast.Boolean(BoolInput);

	[Benchmark]
	public Result<int> Int32Svartalfheim() =>
		IntegerParser.ParseRequired<int>(IntInput, _invariant);

	[Benchmark]
	public Verdict<int> Int32HyperCast() =>
		Cast.Int32(IntInput, NumFormat.Invariant);

	[Benchmark]
	public Result<Guid> GuidSvartalfheim() =>
		GuidParser.ParseRequired(GuidInput);

	[Benchmark]
	public Verdict<Guid> GuidHyperCast() =>
		Cast.Uuid(GuidInput);

	[Benchmark]
	public Result<DateTimeOffset> TimestampSvartalfheim() =>
		DateTimeOffsetParser.ParseRequired(TimestampInput);

	[Benchmark]
	public Verdict<DateTimeOffset> TimestampHyperCast() =>
		Cast.Timestamp(TimestampInput);
}
