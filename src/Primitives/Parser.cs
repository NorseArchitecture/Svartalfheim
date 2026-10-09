using System.Runtime.CompilerServices;
using HyperCast;

namespace Norse.Primitives;

/// <summary>
/// Generic parse gateway over <see cref="ISpanParsable{TSelf}"/>: the bridge from the
/// span world into <see cref="Result{T}"/> with uniform failure semantics.
/// </summary>
/// <remarks>
/// <para>
/// Every type HyperCast has a door for — <see cref="bool"/>, the eleven numerics,
/// <see cref="char"/>, <see cref="Guid"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>,
/// <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/> — is routed by a
/// <c>typeof</c> branch resolved at JIT/AOT compile time into <c>Cast.Scalar&lt;T&gt;</c>, the one
/// engine door; the engine's <c>Verdict&lt;T&gt;</c> is translated into <see cref="Result{T}"/> at
/// this edge and never appears in a Norse signature. The table cannot be a single generic call:
/// the engine door is constrained <c>where T : struct</c> and this gateway on
/// <see cref="ISpanParsable{TSelf}"/>, and C# has no way to narrow one into the other without
/// reflection. Every other type falls through to <c>T.TryParse(span, provider)</c>. There is no
/// runtime registry: a type that cannot parse does not compile.
/// </para>
/// <para>
/// The provider is required and passes straight through to the engine, whose bridge maps a culture's
/// decimal and group separators and currency symbol exactly; the invariant culture, which every live
/// call site declares, maps exactly. The culture-insensitive doors (<see cref="char"/>,
/// <see cref="Guid"/>, the temporal types) ignore it on both sides. The remaining differences between
/// the bridge and <see cref="System.Globalization.NumberFormatInfo"/> (non-ASCII signs, the currency
/// decimal separator, the percent symbol, native digits) are HyperCast's roadmap, not this gateway's.
/// </para>
/// <para>
/// Whitespace: the engine trims on every door but reads a lone <see cref="char"/> before trimming,
/// so the untrimmed span goes to the engine; the failure echo is the trimmed span, bounded by
/// <see cref="Failure"/>. <see cref="DateTime"/> is the timestamp door's instant as
/// <see cref="DateTimeKind.Utc"/>: a zone is mandatory, as it always was here.
/// </para>
/// </remarks>
public static class Parser
{
	/// <summary>
	/// Parses required scalar text. Empty or whitespace input is <see cref="ParseFailure.Empty"/>;
	/// unrecognized input is <see cref="ParseFailure.Malformed"/>; a well-formed value the target
	/// cannot hold is <see cref="ParseFailure.OutOfRange"/>.
	/// </summary>
	/// <typeparam name="T">The target type. Non-nullable by construction.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for culture-sensitive types. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T> ParseRequired<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : ISpanParsable<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (typeof(T) == typeof(bool))
			return Required<bool, T>(input, provider);
		if (typeof(T) == typeof(byte))
			return Required<byte, T>(input, provider);
		if (typeof(T) == typeof(sbyte))
			return Required<sbyte, T>(input, provider);
		if (typeof(T) == typeof(short))
			return Required<short, T>(input, provider);
		if (typeof(T) == typeof(ushort))
			return Required<ushort, T>(input, provider);
		if (typeof(T) == typeof(int))
			return Required<int, T>(input, provider);
		if (typeof(T) == typeof(uint))
			return Required<uint, T>(input, provider);
		if (typeof(T) == typeof(long))
			return Required<long, T>(input, provider);
		if (typeof(T) == typeof(ulong))
			return Required<ulong, T>(input, provider);
		if (typeof(T) == typeof(float))
			return Required<float, T>(input, provider);
		if (typeof(T) == typeof(double))
			return Required<double, T>(input, provider);
		if (typeof(T) == typeof(decimal))
			return Required<decimal, T>(input, provider);
		if (typeof(T) == typeof(char))
			return Required<char, T>(input, provider);
		if (typeof(T) == typeof(Guid))
			return Required<Guid, T>(input, provider);
		if (typeof(T) == typeof(DateOnly))
			return Required<DateOnly, T>(input, provider);
		if (typeof(T) == typeof(TimeOnly))
			return Required<TimeOnly, T>(input, provider);
		if (typeof(T) == typeof(DateTime))
			return Required<DateTime, T>(input, provider);
		if (typeof(T) == typeof(DateTimeOffset))
			return Required<DateTimeOffset, T>(input, provider);
		if (typeof(T) == typeof(TimeSpan))
			return Required<TimeSpan, T>(input, provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, typeof(T).Name) :
			Parse<T>(trimmed, provider);
	}

	/// <summary>
	/// Parses optional scalar text. Empty or whitespace input is absent (<see langword="null"/>);
	/// otherwise exactly <see cref="ParseRequired{T}"/>.
	/// </summary>
	/// <typeparam name="T">The target type. Non-nullable by construction.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for culture-sensitive types. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T>? ParseOptional<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : ISpanParsable<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (typeof(T) == typeof(bool))
			return Optional<bool, T>(input, provider);
		if (typeof(T) == typeof(byte))
			return Optional<byte, T>(input, provider);
		if (typeof(T) == typeof(sbyte))
			return Optional<sbyte, T>(input, provider);
		if (typeof(T) == typeof(short))
			return Optional<short, T>(input, provider);
		if (typeof(T) == typeof(ushort))
			return Optional<ushort, T>(input, provider);
		if (typeof(T) == typeof(int))
			return Optional<int, T>(input, provider);
		if (typeof(T) == typeof(uint))
			return Optional<uint, T>(input, provider);
		if (typeof(T) == typeof(long))
			return Optional<long, T>(input, provider);
		if (typeof(T) == typeof(ulong))
			return Optional<ulong, T>(input, provider);
		if (typeof(T) == typeof(float))
			return Optional<float, T>(input, provider);
		if (typeof(T) == typeof(double))
			return Optional<double, T>(input, provider);
		if (typeof(T) == typeof(decimal))
			return Optional<decimal, T>(input, provider);
		if (typeof(T) == typeof(char))
			return Optional<char, T>(input, provider);
		if (typeof(T) == typeof(Guid))
			return Optional<Guid, T>(input, provider);
		if (typeof(T) == typeof(DateOnly))
			return Optional<DateOnly, T>(input, provider);
		if (typeof(T) == typeof(TimeOnly))
			return Optional<TimeOnly, T>(input, provider);
		if (typeof(T) == typeof(DateTime))
			return Optional<DateTime, T>(input, provider);
		if (typeof(T) == typeof(DateTimeOffset))
			return Optional<DateTimeOffset, T>(input, provider);
		if (typeof(T) == typeof(TimeSpan))
			return Optional<TimeSpan, T>(input, provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			Parse<T>(trimmed, provider);
	}

	// In each JIT-eliminated branch T is statically TDoor; the reinterpret is an identity the type
	// system cannot express (the BCL generic-specialization pattern, benchmark-verified at 1.02×).
	static Result<T> Required<TDoor, T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where TDoor : struct
		where T : notnull
	{
		var routed = Translate(Cast.Scalar<TDoor>(input, provider), input);
		return Unsafe.As<Result<TDoor>, Result<T>>(ref routed);
	}

	// Nullable<X> layout is a function of X alone, so the identity reinterpret holds for the optional
	// shape too. Cast.Optional is the engine's own absent-mapping: Empty becomes null.
	static Result<T>? Optional<TDoor, T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where TDoor : struct
		where T : notnull
	{
		Result<TDoor>? routed = Cast.Optional(Cast.Scalar<TDoor>(input, provider)) is { } verdict ?
			Translate(verdict, input) :
			null;
		return Unsafe.As<Result<TDoor>?, Result<T>?>(ref routed);
	}

	// The engine edge: a Verdict is a Result, member for member — the reason reinterprets by number
	// (ParseFailure mirrors CastFailure), the echo is the forge's bounded, trimmed capture.
	// HyperCast.Success<T> is written qualified because the forge's own Success<T> owns the bare name
	// in this namespace.
	static Result<TDoor> Translate<TDoor>(Verdict<TDoor> verdict, ReadOnlySpan<char> input)
		where TDoor : struct
	{
		if (verdict.TryGetValue(out HyperCast.Success<TDoor> success))
			return new Success<TDoor>(success.Value);
		verdict.TryGetValue(out Fault fault);
		return new Failure((ParseFailure)fault.Reason, input.Trim(), typeof(TDoor).Name);
	}

	static Result<T> Parse<T>(ReadOnlySpan<char> trimmed, IFormatProvider provider)
		where T : ISpanParsable<T> =>
		T.TryParse(trimmed, provider, out var value) ?
			new Success<T>(value) :
			new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
}
