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
/// Range is the type's own — <c>byte "256"</c> is <see cref="ParseFailure.Malformed"/> for free.
/// A decimal point is never accepted on an integer, so <c>1e3</c> parses to 1000 but
/// <c>1.5e0</c> and <c>1.5e3</c> are malformed. Hex is read as the two's-complement bit pattern,
/// so <c>0xFF</c> is <c>-1</c> for <see cref="sbyte"/>. The provider is required and non-nullable
/// (numeric grouping and currency are culture-sensitive); the radix forms ignore it by nature.
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
		where T : IBinaryInteger<T> =>
		TryRadix<T>(trimmed, out var radix) ?
			new Success<T>(radix) :
			T.TryParse(trimmed, DecimalStyles, provider, out var value) ?
				new Success<T>(value) :
				new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);

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
