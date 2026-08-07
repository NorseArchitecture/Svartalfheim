namespace Norse.Primitives.Benchmarks;

[MemoryDiagnoser]
public class DispatchBenchmarks
{
	const string BoolInput = "yes";
	const string IntInput = "1742";

	static readonly IFormatProvider _invariant = CultureInfo.InvariantCulture;

	[Benchmark(Baseline = true)]
	public Result<bool> DirectSpecialist() =>
		BooleanParser.ParseRequired(BoolInput);

	[Benchmark]
	public Result<bool> GatewayBool() =>
		Parser.ParseRequired<bool>(BoolInput, _invariant);

	[Benchmark]
	public Result<int> GatewayInt() =>
		Parser.ParseRequired<int>(IntInput, _invariant);
}
