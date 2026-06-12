using System.Globalization;
using BenchmarkDotNet.Attributes;

namespace Norse.Primitives.Benchmarks;

[MemoryDiagnoser]
public class CombinatorBenchmarks
{
	static readonly Func<int, int> _addEleven = x => x + 11;

	static readonly Func<int, Result<int>> _doubleOdd = x =>
	{
		if (x % 2 == 1)
			return new Success<int>(x * 2);
		return new Failure(ParseFailure.Malformed, "even", "Int32");
	};

	static readonly Func<int, string> _renderValue = x => x.ToString(CultureInfo.InvariantCulture);

	static readonly Func<Failure, string> _renderFailure = failure => failure.Reason.ToString();

	readonly int _seed = 10;

	[Benchmark(Baseline = true)]
	public string HandRolledSwitch()
	{
		Result<int> seeded = new Success<int>(_seed);
		if (!seeded.TryGetValue(out Success<int> first))
			return RenderFailureOf(seeded);
		var bound = _doubleOdd(_addEleven(first.Value));
		return bound.TryGetValue(out Success<int> second)
			? _renderValue(second.Value)
			: RenderFailureOf(bound);
	}

	[Benchmark]
	public string CombinatorChain()
	{
		Result<int> seeded = new Success<int>(_seed);
		return seeded.Map(_addEleven).Bind(_doubleOdd).Match(_renderValue, _renderFailure);
	}

	static string RenderFailureOf(Result<int> result) =>
		result.TryGetValue(out Failure failure) ? _renderFailure(failure) : "default";
}
