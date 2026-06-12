namespace Norse.Primitives.Benchmarks;

/// <summary>
/// The road not taken: a <see cref="Result{T}"/> twin that boxes its case into a single
/// object field. Exists only as the storage A/B comparator (pathway-proof spec §4.1) —
/// it is never shipped and never grows.
/// </summary>
public readonly record struct BoxedResult<T> where T : notnull
{
	readonly object? _value;

	public BoxedResult(Success<T> value) => _value = value;

	public BoxedResult(Failure value)
	{
		if (value.Reason == ParseFailure.Unspecified)
			throw new ArgumentOutOfRangeException(nameof(value), value.Reason, "Failure must carry a real reason; default(Failure) is not a valid case value.");
		_value = value;
	}

	public bool TryGetValue(out Success<T> value)
	{
		if (_value is Success<T> success)
		{
			value = success;
			return true;
		}
		value = default;
		return false;
	}

	public bool TryGetValue(out Failure value)
	{
		if (_value is Failure failure)
		{
			value = failure;
			return true;
		}
		value = default;
		return false;
	}
}
