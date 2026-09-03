using HyperUuid;
using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Benchmarks;

// HyperUuid identifier benchmarks -- proves the native path (wired behind NativeCapability in
// src/Primitives/Identifiers) stays faster than the managed fallback. Permanent fixture: run
// before/after any change to either engine's identifier generation.
[MemoryDiagnoser]
public class IdentifierBenchmarks
{
	const string Name = "example.com";
	static readonly Guid _dnsNamespace = DeterministicGuid.Namespaces.Dns;

	readonly SequentialGuid _rfcSequential = new();
	readonly Guid _hyperV7 = UuidGenerator.NewV7();
	readonly SequentialGuid[] _sequentialBatch = new SequentialGuid[1000];
	readonly Guid[] _hyperBatch = new Guid[1000];

	[Benchmark(Baseline = true)]
	public SequentialGuid GenerateV7Svartalfheim() =>
		new();

	[Benchmark]
	public Guid GenerateV7HyperUuid() =>
		UuidGenerator.NewV7();

	[Benchmark]
	public void FillBatch1000Svartalfheim() =>
		SequentialGuid.Fill(_sequentialBatch);

	[Benchmark]
	public void FillBatch1000HyperUuid() =>
		UuidGenerator.FillV7(_hyperBatch);

	[Benchmark]
	public DeterministicGuid GenerateV5Svartalfheim() =>
		new(_dnsNamespace, Name);

	[Benchmark]
	public Guid GenerateV5HyperUuid() =>
		UuidGenerator.NewV5(_dnsNamespace, Name);

	[Benchmark]
	public SequentialGuid SqlOrderRoundTripSvartalfheim() =>
		_rfcSequential.ToSqlOrder().ToRfcOrder();

	[Benchmark]
	public Guid SqlOrderRoundTripHyperUuid() =>
		UuidGenerator.V7FromSqlOrder(UuidGenerator.V7ToSqlOrder(_hyperV7));
}
