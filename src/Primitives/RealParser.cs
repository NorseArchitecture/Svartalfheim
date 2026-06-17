using System.Globalization;
using System.Numerics;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for the real family (<see cref="float"/>, <see cref="double"/>,
/// <see cref="decimal"/> — every <see cref="IFloatingPoint{TSelf}"/>). Extends the bare parse with
/// provider-declared thousands grouping and currency, accounting parentheses, exponent form, and
/// trailing-percentage notation (<c>50%</c> → <c>0.5</c>).
/// </summary>
/// <remarks>
/// <para>
/// The forge admits only finite reals. <c>NaN</c>, <c>Infinity</c>, and <c>-Infinity</c> are
/// <see cref="ParseFailure.Malformed"/> — whether they arrive as the literal symbol or as the
/// result of magnitude overflow (<c>1e400</c> → <c>Infinity</c> → rejected). The finite check is
/// <see cref="System.Numerics.INumberBase{TSelf}.IsFinite"/>; it is a no-op for
/// <see cref="decimal"/> (which has no non-finite values, and whose overflow already fails
/// <c>TryParse</c>). Overflow fails loud uniformly across all three real types.
/// </para>
/// <para>
/// A <see cref="decimal"/> with more than 29 digit characters is rejected up front: no in-range
/// decimal carries that many significant digits, and the guard turns a silent round-to-zero into a
/// loud failure. The guard is <see cref="decimal"/>-only — the <c>typeof</c> test is eliminated for
/// the IEEE types, which carry far more magnitude. The provider is required and non-nullable.
/// </para>
/// </remarks>
public static class RealParser
{
	const NumberStyles RealStyles =
		NumberStyles.Number
		| NumberStyles.AllowExponent
		| NumberStyles.AllowParentheses
		| NumberStyles.AllowCurrencySymbol;

	const int DecimalDigitGuard = 29;

	/// <summary>
	/// Parses required real text. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; unrecognized input is
	/// <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target floating-point type.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for grouping, decimal point, and currency. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T> ParseRequired<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : notnull, IFloatingPoint<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, typeof(T).Name);
		return Parse<T>(trimmed, provider);
	}

	/// <summary>
	/// Parses optional real text. Empty or whitespace input is absent
	/// (<see langword="null"/>); unrecognized input is <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target floating-point type.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for grouping, decimal point, and currency. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T>? ParseOptional<T>(ReadOnlySpan<char> input, IFormatProvider provider)
		where T : notnull, IFloatingPoint<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return Parse<T>(trimmed, provider);
	}

	static Result<T> Parse<T>(ReadOnlySpan<char> trimmed, IFormatProvider provider)
		where T : notnull, IFloatingPoint<T>
	{
		if (typeof(T) == typeof(decimal) && CountDigits(trimmed) > DecimalDigitGuard)
			return new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
		if (trimmed[^1] == '%')
		{
			var body = trimmed[..^1].TrimEnd();
			if (T.TryParse(body, RealStyles, provider, out var percent) && T.IsFinite(percent!))
				return new Success<T>(percent! / T.CreateChecked(100));
			return new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
		}
		if (T.TryParse(trimmed, RealStyles, provider, out var value) && T.IsFinite(value!))
			return new Success<T>(value!);
		return new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
	}

	static int CountDigits(ReadOnlySpan<char> span)
	{
		var count = 0;
		foreach (var character in span)
			if (char.IsAsciiDigit(character))
				count++;
		return count;
	}
}
