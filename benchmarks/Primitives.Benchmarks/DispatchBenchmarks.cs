using HyperCast;

namespace Norse.Primitives.Benchmarks;

// The forge's wrapping tax over the engine: a direct engine call against the gateway's routed,
// translated Result<T>. The September seam record put the tax at ~40 ns; the engines cut should hold
// or lower it. Baseline is the direct bool door, as DirectSpecialist was before the cut.
[MemoryDiagnoser]
public class DispatchBenchmarks
{
	const string BoolInput = "yes";
	const string IntInput = "1742";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Benchmark(Baseline = true)]
	public Verdict<bool> DirectEngineBool() =>
		Cast.Scalar<bool>(BoolInput, _invariant);

	[Benchmark]
	public Result<bool> GatewayBool() =>
		Parser.ParseRequired<bool>(BoolInput, _invariant);

	[Benchmark]
	public Verdict<int> DirectEngineInt() =>
		Cast.Scalar<int>(IntInput, _invariant);

	[Benchmark]
	public Result<int> GatewayInt() =>
		Parser.ParseRequired<int>(IntInput, _invariant);
}
