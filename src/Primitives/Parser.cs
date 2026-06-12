using System.Runtime.CompilerServices;

namespace Norse.Primitives;

/// <summary>
/// Generic parse gateway over <see cref="ISpanParsable{TSelf}"/>: the bridge from the
/// span world into <see cref="Result{T}"/> with uniform failure semantics.
/// </summary>
/// <remarks>
/// <para>
/// Hot-path specialists are routed by <c>typeof</c> branches resolved at JIT/AOT compile
/// time — <see cref="bool"/> routes to <see cref="BooleanParser"/>, whose richer vocabulary
/// the bare <see cref="bool.TryParse(ReadOnlySpan{char}, out bool)"/> lacks; the provider is
/// deliberately not forwarded there (boolean text is culture-insensitive). Every other type
/// parses through its own <see cref="ISpanParsable{TSelf}"/> implementation. There is no
/// runtime registry: a type that cannot parse does not compile.
/// Leading and trailing whitespace is trimmed on every route. The provider null-check is
/// uniform — it precedes specialist routing, so even culture-insensitive routes demand a
/// declared culture at the call site.
/// </para>
/// <para>
/// The provider is required. A call site parsing culture-sensitive text declares its culture
/// out loud (e.g. <see cref="System.Globalization.CultureInfo.InvariantCulture"/>) or it does
/// not compile — there is no defaulting overload.
/// </para>
/// </remarks>
public static class Parser
{
	/// <summary>
	/// Parses required scalar text. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; unrecognized input is
	/// <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target type. Non-nullable by construction.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for culture-sensitive types. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T> ParseRequired<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : notnull, ISpanParsable<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (typeof(T) == typeof(bool))
		{
			// In this JIT-eliminated branch T is statically bool; the reinterpret is an
			// identity the type system cannot express (the BCL generic-specialization pattern).
			var routed = BooleanParser.ParseRequired(input);
			return Unsafe.As<Result<bool>, Result<T>>(ref routed);
		}
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, typeof(T).Name);
		return Parse<T>(trimmed, provider);
	}

	/// <summary>
	/// Parses optional scalar text. Empty or whitespace input is absent
	/// (<see langword="null"/>); unrecognized input is <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target type. Non-nullable by construction.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for culture-sensitive types. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T>? ParseOptional<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : notnull, ISpanParsable<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (typeof(T) == typeof(bool))
		{
			// Identity reinterpret as in ParseRequired; Nullable<X> layout is a function of X alone.
			var routed = BooleanParser.ParseOptional(input);
			return Unsafe.As<Result<bool>?, Result<T>?>(ref routed);
		}
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return Parse<T>(trimmed, provider);
	}

	static Result<T> Parse<T>(ReadOnlySpan<char> trimmed, IFormatProvider provider)
		where T : notnull, ISpanParsable<T>
	{
		if (T.TryParse(trimmed, provider, out var value))
			return new Success<T>(value);
		return new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
	}
}
