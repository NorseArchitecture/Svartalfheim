using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for the binary-integer family (<see cref="byte"/> through
/// <see cref="ulong"/>). Extends the bare <see cref="IBinaryInteger{TSelf}"/> parse with the
/// notations untrusted sources actually send: provider-declared thousands grouping and currency,
/// accounting parentheses, exponent form, and culture-insensitive hex (<c>0x</c>/<c>&amp;H</c>)
/// and binary (<c>0b</c>) radix prefixes.
/// </summary>
/// <remarks>
/// Range is the type's own — <c>byte "256"</c> is <see cref="ParseFailure.OutOfRange"/>: the text
/// is numerically well-formed but exceeds <c>T</c>'s range, distinguished on both engines from a
/// genuinely unrecognizable <see cref="ParseFailure.Malformed"/> input. A decimal point is never
/// accepted on an integer, so <c>1e3</c> parses to 1000 but <c>1.5e0</c> and <c>1.5e3</c> are
/// malformed. Hex is read as the two's-complement bit pattern, so <c>0xFF</c> is <c>-1</c> for
/// <see cref="sbyte"/>. The provider is required and non-nullable (numeric grouping and currency
/// are culture-sensitive); the radix forms ignore it by nature.
/// </remarks>
public static class IntegerParser
{
	const NumberStyles DecimalStyles =
		NumberStyles.Integer |
		NumberStyles.AllowThousands |
		NumberStyles.AllowParentheses | NumberStyles.AllowCurrencySymbol |
		NumberStyles.AllowExponent;

	/// <summary>
	/// Parses required integer text. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; unrecognized input is
	/// <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target binary-integer type.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for grouping and currency. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T> ParseRequired<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : IBinaryInteger<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, typeof(T).Name) :
			Parse<T>(trimmed, provider);
	}

	/// <summary>
	/// Parses optional integer text. Empty or whitespace input is absent
	/// (<see langword="null"/>); unrecognized input is <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target binary-integer type.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for grouping and currency. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T>? ParseOptional<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : IBinaryInteger<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			Parse<T>(trimmed, provider);
	}

	static Result<T> Parse<T>(ReadOnlySpan<char> trimmed, IFormatProvider provider)
		where T : IBinaryInteger<T>
	{
		// HyperCast's native door has no currency-symbol concept and always parses under its own
		// fixed invariant NumFormat -- it cannot honor a caller-declared culture's grouping,
		// currency symbol, or negative-parenthesization conventions. Routing there is only sound
		// when the caller's own provider is itself culture-invariant; any other provider (e.g.
		// en-US currency parsing) must fall through to the managed T.TryParse path below, which
		// does honor the provider.
		if (NativeCapability.Available && IsInvariant(provider) && TryParseNative<T>(trimmed, out var nativeResult))
			return nativeResult;

		if (TryRadix<T>(trimmed, out var radix))
			return new Success<T>(radix);
		if (T.TryParse(trimmed, DecimalStyles, provider, out var value))
			return new Success<T>(value);

		// T rejected it -- was the text numerically well-formed but out of T's range, or
		// genuinely not a number at all? BigInteger has no practical ceiling, so a successful
		// BigInteger parse under the same styles/provider proves the text itself was fine.
		return BigInteger.TryParse(trimmed, DecimalStyles, provider, out _) ?
			new Failure(ParseFailure.OutOfRange, trimmed, typeof(T).Name) :
			new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
	}

	static bool IsInvariant(IFormatProvider provider) =>
		ReferenceEquals(NumberFormatInfo.GetInstance(provider), NumberFormatInfo.InvariantInfo);

	// Every IBinaryInteger<T> this parser supports gets its own HyperCast door -- there is no
	// generic native entry point, so this dispatches on typeof(T) once per call. Each concrete
	// instantiation of Parse<T> JIT-compiles with a single true branch here (the same
	// typeof(T)-branch-elimination pattern Parser's own bool routing already relies on),
	// so this costs nothing at runtime for any one T.
	static bool TryParseNative<T>(ReadOnlySpan<char> trimmed, out Result<T> result) where T : IBinaryInteger<T>
	{
		var format = HyperCast.NumFormat.Invariant;
		switch (typeof(T))
		{
			case Type t when t == typeof(sbyte):
				result = Translate<T, sbyte>(HyperCast.Cast.SByte(trimmed, format), trimmed);
				return true;
			case Type t when t == typeof(short):
				result = Translate<T, short>(HyperCast.Cast.Int16(trimmed, format), trimmed);
				return true;
			case Type t when t == typeof(int):
				result = Translate<T, int>(HyperCast.Cast.Int32(trimmed, format), trimmed);
				return true;
			case Type t when t == typeof(long):
				result = Translate<T, long>(HyperCast.Cast.Int64(trimmed, format), trimmed);
				return true;
			case Type t when t == typeof(byte):
				result = Translate<T, byte>(HyperCast.Cast.Byte(trimmed, format), trimmed);
				return true;
			case Type t when t == typeof(ushort):
				result = Translate<T, ushort>(HyperCast.Cast.UInt16(trimmed, format), trimmed);
				return true;
			case Type t when t == typeof(uint):
				result = Translate<T, uint>(HyperCast.Cast.UInt32(trimmed, format), trimmed);
				return true;
			case Type t when t == typeof(ulong):
				result = Translate<T, ulong>(HyperCast.Cast.UInt64(trimmed, format), trimmed);
				return true;
			default:
				result = default!;
				return false;
		}
	}

	static Result<T> Translate<T, TNative>(HyperCast.Verdict<TNative> verdict, ReadOnlySpan<char> trimmed)
		where T : IBinaryInteger<T>
		where TNative : struct, IBinaryInteger<TNative>
	{
		if (verdict.TryGetValue(out HyperCast.Success<TNative> success))
			return new Success<T>(T.CreateChecked(success.Value));

		verdict.TryGetValue(out HyperCast.Fault fault);
		return fault.Reason == HyperCast.CastFailure.OutOfRange ?
			new Failure(ParseFailure.OutOfRange, trimmed, typeof(T).Name) :
			new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
	}

	static bool TryRadix<T>(ReadOnlySpan<char> trimmed, [MaybeNullWhen(false)] out T value) where T : notnull, IBinaryInteger<T>
	{
		if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
			trimmed.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
			return T.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
		if (trimmed.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
			return T.TryParse(trimmed[2..], NumberStyles.BinaryNumber, CultureInfo.InvariantCulture, out value);
		value = T.Zero;
		return false;
	}
}
