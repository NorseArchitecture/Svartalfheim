namespace Norse.Primitives.Benchmarks;

[MemoryDiagnoser]
public class StorageBenchmarks
{
	const bool Flag = true;
	static readonly Failure _malformedBoolean = new(ParseFailure.Malformed, "bogus", "Boolean");

	[Benchmark(Baseline = true)]
	public bool InlineSuccess()
	{
		var result = CreateInlineSuccess(Flag);
		return result.TryGetValue(out Success<bool> success) && success.Value;
	}

	[Benchmark]
	public bool BoxedSuccess()
	{
		var result = CreateBoxedSuccess(Flag);
		return result.TryGetValue(out Success<bool> success) && success.Value;
	}

	[Benchmark]
	public ParseFailure InlineFailure()
	{
		var result = CreateInlineFailure();
		return result.TryGetValue(out Failure failure) ? failure.Reason : ParseFailure.Unspecified;
	}

	[Benchmark]
	public ParseFailure BoxedFailure()
	{
		var result = CreateBoxedFailure();
		return result.TryGetValue(out Failure failure) ? failure.Reason : ParseFailure.Unspecified;
	}

	// Returned values escape the constructing frame, so the boxed twin must heap-allocate —
	// exactly as it would when a real parser returns a result across a method boundary.
	// Without this boundary, .NET 11 escape analysis stack-allocates the box and the
	// Allocated column files false evidence (0 B for a design that allocates per result).
	[MethodImpl(MethodImplOptions.NoInlining)]
	static Result<bool> CreateInlineSuccess(bool flag) =>
		new Success<bool>(flag);

	[MethodImpl(MethodImplOptions.NoInlining)]
	static BoxedResult<bool> CreateBoxedSuccess(bool flag) =>
		new(new Success<bool>(flag));

	[MethodImpl(MethodImplOptions.NoInlining)]
	static Result<bool> CreateInlineFailure() =>
		_malformedBoolean;

	[MethodImpl(MethodImplOptions.NoInlining)]
	static BoxedResult<bool> CreateBoxedFailure() =>
		new(_malformedBoolean);
}
