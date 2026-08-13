using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Norse.Primitives;

/// <summary>
/// The outcome of a scalar→domain conversion: exactly one of
/// <see cref="Success{T}"/> or <see cref="Failure"/>, as a native C# union.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pattern matching unwraps to the case types.</b> Match against
/// <see cref="Success{T}"/> or <see cref="Failure"/> — never against
/// <c>Result&lt;T&gt;</c> itself; the compiler rejects <c>result is Result&lt;T&gt;</c> (CS8121).
/// A two-arm switch over both case types is exhaustive.
/// </para>
/// <para>
/// <b>Do not use <c>default(Result&lt;T&gt;)</c> or <c>new Result&lt;T&gt;()</c>.</b>
/// Like <c>default(ImmutableArray&lt;T&gt;)</c>, a defaulted value is malformed by
/// construction: union well-formedness requires its <see cref="Value"/> to be null,
/// so it matches neither case and an exhaustive switch throws
/// <see cref="SwitchExpressionException"/> at first consumption.
/// </para>
/// <para>
/// This type hand-implements the union pattern (rather than using a shorthand
/// <c>union</c> declaration) so both cases are stored inline and nothing boxes
/// on either path. The compiler routes pattern matching through
/// <see cref="TryGetValue(out Success{T})"/> / <see cref="TryGetValue(out Failure)"/>;
/// only a direct read of <see cref="Value"/> boxes.
/// </para>
/// </remarks>
/// <typeparam name="T">The validated value's type. Non-nullable by construction.</typeparam>
[MustConsume]
[Union]
public readonly record struct Result<T> : IUnion where T : notnull
{
	enum State : byte
	{
		Default = 0,
		Success = 1,
		Failure = 2
	}

	readonly Success<T> _success;
	readonly Failure _failure;
	readonly State _state;

	/// <summary>Creates a successful result. Also, reachable as an implicit union conversion.</summary>
	/// <param name="value">The validated value.</param>
	public Result(Success<T> value)
	{
		_success = value;
		_state = State.Success;
	}

	/// <summary>Creates a failed result. Also, reachable as an implicit union conversion.</summary>
	/// <param name="value">The conversion failure.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="value"/> carries the <see cref="ParseFailure.Unspecified"/> sentinel —
	/// a <c>default(Failure)</c> smuggled past the <see cref="Failure"/> constructor guards.
	/// </exception>
	public Result(Failure value)
	{
		if (value.Reason == ParseFailure.Unspecified)
			throw new ArgumentOutOfRangeException(nameof(value), value.Reason, "Failure must carry a real reason; default(Failure) is not a valid case value.");
		_failure = value;
		_state = State.Failure;
	}

	/// <summary>
	/// Wraps a validated value as the success case. The second legitimate author of the union
	/// (spec 2026-08-02-result-success-unwrap-on-serialize §2): a first-party client holding a
	/// compile-time-typed value states it as plain assignment.
	/// </summary>
	/// <param name="value">The validated value.</param>
	[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates",
		Justification =
			"Deliberately narrow public surface: this union's starved API is the design, and a named FromT/ToResult alternate would widen the surface the type exists to narrow. Construction ergonomics -- plain assignment for a first-party client holding a compile-time-typed value -- is this operator's whole purpose.")]
	public static implicit operator Result<T>(T value) =>
		new(new Success<T>(value));

	/// <summary>
	/// The boxed case contents, or <see langword="null"/> for a defaulted value.
	/// Pattern matching does not read this property; a direct read boxes.
	/// </summary>
	public object? Value =>
		_state switch
		{
			State.Success => _success,
			State.Failure => _failure,
			_ => null,
		};

	/// <summary><see langword="true"/> unless this value was defaulted rather than constructed.</summary>
	public bool HasValue =>
		_state != State.Default;

	/// <summary>Retrieves the success case without boxing.</summary>
	/// <param name="value">The success case when present; default otherwise.</param>
	/// <returns><see langword="true"/> if this result is the success case.</returns>
	public bool TryGetValue(out Success<T> value)
	{
		value = _success;
		return _state == State.Success;
	}

	/// <summary>Retrieves the failure case without boxing.</summary>
	/// <param name="value">The failure case when present; default otherwise.</param>
	/// <returns><see langword="true"/> if this result is the failure case.</returns>
	public bool TryGetValue(out Failure value)
	{
		value = _failure;
		return _state == State.Failure;
	}

	/// <summary>Transforms the success value; a failure flows through untouched.</summary>
	/// <remarks>
	/// Combinators are composition ergonomics, not the hot path — row-volume loops
	/// switch over the cases directly. Nothing here allocates beyond the caller's
	/// own closures.
	/// </remarks>
	/// <typeparam name="TResult">The transformed value's type. Non-nullable by construction.</typeparam>
	/// <param name="selector">The success-case transform. Exceptions it throws propagate unhandled.</param>
	/// <returns>The transformed result.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
	/// <exception cref="SwitchExpressionException">This value was defaulted rather than constructed.</exception>
	public Result<TResult> Map<TResult>(Func<T, TResult> selector) where TResult : notnull
	{
		ArgumentNullException.ThrowIfNull(selector);
		if (TryGetValue(out Success<T> success))
			return new Success<TResult>(selector(success.Value));
		if (TryGetValue(out Failure failure))
			return failure;
		throw new SwitchExpressionException(this);
	}

	/// <summary>Chains a dependent conversion; a failure flows through untouched.</summary>
	/// <remarks>
	/// Combinators are composition ergonomics, not the hot path — row-volume loops
	/// switch over the cases directly. Nothing here allocates beyond the caller's
	/// own closures.
	/// </remarks>
	/// <typeparam name="TResult">The chained result's value type. Non-nullable by construction.</typeparam>
	/// <param name="binder">The success-case continuation. Exceptions it throws propagate unhandled.</param>
	/// <returns>The chained result.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="binder"/> is null.</exception>
	/// <exception cref="SwitchExpressionException">This value was defaulted rather than constructed.</exception>
	public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binder) where TResult : notnull
	{
		ArgumentNullException.ThrowIfNull(binder);
		if (TryGetValue(out Success<T> success))
			return binder(success.Value);
		if (TryGetValue(out Failure failure))
			return failure;
		throw new SwitchExpressionException(this);
	}

	/// <summary>Consumes the result by handling both cases.</summary>
	/// <typeparam name="TResult">The handlers' common return type.</typeparam>
	/// <param name="success">The success-case handler. Exceptions it throws propagate unhandled.</param>
	/// <param name="failure">The failure-case handler. Exceptions it throws propagate unhandled.</param>
	/// <returns>Whichever handler ran.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="success"/> or <paramref name="failure"/> is null.</exception>
	/// <exception cref="SwitchExpressionException">This value was defaulted rather than constructed.</exception>
	public TResult Match<TResult>(Func<T, TResult> success, Func<Failure, TResult> failure)
	{
		ArgumentNullException.ThrowIfNull(success);
		ArgumentNullException.ThrowIfNull(failure);
		if (TryGetValue(out Success<T> s))
			return success(s.Value);
		if (TryGetValue(out Failure f))
			return failure(f);
		throw new SwitchExpressionException(this);
	}

	/// <summary>Renders "Success(value)", "Failure(Reason, "input")", or "Default(invalid)".</summary>
	public override string ToString() =>
		_state switch
		{
			State.Success => $"Success({_success.Value})",
			State.Failure => $"Failure({_failure.Reason}, \"{_failure.Input}\")",
			_ => "Default(invalid)",
		};
}
