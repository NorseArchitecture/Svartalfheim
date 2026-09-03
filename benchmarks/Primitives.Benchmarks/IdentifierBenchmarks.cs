using HyperUuid;
using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Benchmarks;

// HyperUuid blast-radius assessment (2026-09-03) -- head-to-head against this realm's own
// zero-alloc managed Identifiers, the question benchmarks/Primitives.Benchmarks had no
// evidence for yet. Not a permanent fixture: remove alongside the HyperUuid
// PackageReference once the adoption question is settled either way.
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
