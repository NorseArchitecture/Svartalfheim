using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Benchmarks;

// The four identifier doors the September record measured (the superseded seam spec §1): run once
// on the managed implementation before the engines cut, once on the engine after — same machine,
// same configuration — and filed side by side in the pathway spec's §10 amendment.
[MemoryDiagnoser]
public class IdentifierBenchmarks
{
	static readonly SequentialGuid _rfc = new();
	readonly SequentialGuid[] _batch = new SequentialGuid[1000];

	[Benchmark(Baseline = true)]
	public SequentialGuid GenerateV7() =>
		new();

	[Benchmark]
	public void FillBatch1000() =>
		SequentialGuid.Fill(_batch);

	[Benchmark]
	public DeterministicGuid DeriveV5() =>
		new(DeterministicGuid.Namespaces.Dns, "example.com");

	[Benchmark]
	public SequentialGuid SqlOrderRoundTrip() =>
		_rfc.ToSqlOrder().ToRfcOrder();
}
