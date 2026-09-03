namespace Norse.Primitives.Tests;

/// <summary>
/// Corpus-driven conformance: every vector in HyperCast's own vendored test corpus, run through
/// Svartálfheim's parsers on both the native (HyperCast) and forced-managed paths. Proves the two
/// paths agree with HyperCast's own grammar, not just with each other. First doors proven:
/// Boolean and Guid — every subsequent parser task's corpus tests follow this same shape.
/// </summary>
// Runs in NativeCapabilityCollection: the managed-path theories force NativeCapability.Available
// via ForManagedOnly, which mutates thread-local state that must not race another test reading
// NativeCapability.Available concurrently.
[Collection(nameof(NativeCapabilityCollection))]
public sealed class CorpusConformanceTests
{
	public static IEnumerable<object[]> BooleanVectors() => CorpusVector.Load("boolean.json");
	public static IEnumerable<object[]> GuidVectors() => CorpusVector.Load("uuid.json");

	[Theory]
	[MemberData(nameof(BooleanVectors))]
	void Boolean_native_path_matches_the_corpus(CorpusVector vector) =>
		AssertBooleanMatchesCorpus(vector);

	[Theory]
	[MemberData(nameof(BooleanVectors))]
	void Boolean_managed_path_matches_the_corpus(CorpusVector vector) =>
		NativeCapability.ForManagedOnly(() => AssertBooleanMatchesCorpus(vector));

	[Theory]
	[MemberData(nameof(GuidVectors))]
	void Guid_native_path_matches_the_corpus(CorpusVector vector) =>
		AssertGuidMatchesCorpus(vector);

	[Theory]
	[MemberData(nameof(GuidVectors))]
	void Guid_managed_path_matches_the_corpus(CorpusVector vector) =>
		NativeCapability.ForManagedOnly(() => AssertGuidMatchesCorpus(vector));

	static void AssertBooleanMatchesCorpus(CorpusVector vector)
	{
		var result = BooleanParser.ParseRequired(vector.Input);
		if (vector.ExpectSuccess)
			result.TryGetValue(out Success<bool> _).ShouldBeTrue();
		else
			result.TryGetValue(out Failure _).ShouldBeTrue();
	}

	static void AssertGuidMatchesCorpus(CorpusVector vector)
	{
		var result = GuidParser.ParseRequired(vector.Input);
		if (vector.ExpectSuccess)
			result.TryGetValue(out Success<Guid> _).ShouldBeTrue();
		else
			result.TryGetValue(out Failure _).ShouldBeTrue();
	}
}
