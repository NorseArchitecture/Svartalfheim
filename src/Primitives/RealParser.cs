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
/// The forge admits only finite reals. A genuinely non-numeric non-finite token (the literal
/// <c>NaN</c>, <c>Infinity</c>, or <c>-Infinity</c> symbol) is <see cref="ParseFailure.Malformed"/>
/// — it carries no digit at all, so it is never mistaken for a magnitude problem. A numerically
/// well-formed literal whose magnitude simply exceeds <c>T</c>'s finite range (<c>1e400</c> for
/// <see cref="double"/>, <c>1e39</c> for <see cref="float"/>) is <see cref="ParseFailure.OutOfRange"/>
/// instead — the same well-formed-but-out-of-range distinction <see cref="IntegerParser"/> draws via
/// its <see cref="System.Numerics.BigInteger"/> fallback, and the same distinction HyperCast's own
/// native <c>Cast.Single</c>/<c>Cast.Double</c> draw. The finite check is
/// <see cref="System.Numerics.INumberBase{TSelf}.IsFinite"/>; it is a no-op for
/// <see cref="decimal"/> (which has no non-finite values, and whose overflow already fails
/// <c>TryParse</c>).
/// </para>
/// <para>
/// A <see cref="decimal"/> with more than 29 digit characters is rejected up front: no in-range
/// decimal carries that many significant digits, and the guard turns a silent round-to-zero into a
/// loud failure. The guard is <see cref="decimal"/>-only — the <c>typeof</c> test is eliminated for
/// the IEEE types, which carry far more magnitude. The provider is required and non-nullable.
/// </para>
/// <para>
/// <see cref="ParseRequired{T}(ReadOnlySpan{char}, IFormatProvider, bool)"/>'s <c>detectSeparators</c>
/// door is HyperCast's <c>NumFormat.Detect</c> mirrored into managed code: a caller-declared,
/// off-by-default opt-in that resolves which of <c>.</c>/<c>,</c> is the decimal separator and
/// which is grouping from the input's own structure, instead of trusting the caller's declared
/// culture to say so. It never changes behavior for a caller who leaves it
/// <see langword="false"/> and simply declares a normal culture.
/// </para>
/// </remarks>
public static class RealParser
{
	const NumberStyles RealStyles =
		NumberStyles.Number |
		NumberStyles.AllowExponent |
		NumberStyles.AllowParentheses |
		NumberStyles.AllowCurrencySymbol;

	const int DecimalDigitGuard = 29;

	/// <summary>
	/// Parses required real text. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; unrecognized input is
	/// <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target floating-point type.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for grouping, decimal point, and currency. Never null.</param>
	/// <param name="detectSeparators">
	/// Caller-declared opt-in, off by default: when <see langword="true"/>, the <c>.</c>/<c>,</c>
	/// roles are resolved structurally from the input itself (HyperCast's <c>NumFormat.Detect</c>)
	/// instead of from <paramref name="provider"/> — a repeated separator is grouping, the rightmost
	/// of two distinct separators is decimal, a non-3-digit run to the right of a lone separator is
	/// decimal, a leading-zero integer part before a 3-digit run is decimal, and a genuinely
	/// ambiguous case (<c>12.185</c>, <c>1,000</c>) is <see cref="ParseFailure.Malformed"/> rather
	/// than guessed. Leave <see langword="false"/> for every caller who declares a normal culture.
	/// </param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T> ParseRequired<T>(ReadOnlySpan<char> input, IFormatProvider provider, bool detectSeparators = false)
		where T : IFloatingPoint<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, typeof(T).Name) :
			Parse<T>(trimmed, provider, detectSeparators);
	}

	/// <summary>
	/// Parses optional real text. Empty or whitespace input is absent
	/// (<see langword="null"/>); unrecognized input is <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <typeparam name="T">The target floating-point type.</typeparam>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="provider">The declared culture for grouping, decimal point, and currency. Never null.</param>
	/// <param name="detectSeparators">
	/// Caller-declared opt-in, off by default — see
	/// <see cref="ParseRequired{T}(ReadOnlySpan{char}, IFormatProvider, bool)"/> for the resolution
	/// rules.
	/// </param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<T>? ParseOptional<T>(ReadOnlySpan<char> input, IFormatProvider provider, bool detectSeparators = false)
		where T : IFloatingPoint<T>
	{
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			Parse<T>(trimmed, provider, detectSeparators);
	}

	static Result<T> Parse<T>(ReadOnlySpan<char> trimmed, IFormatProvider provider, bool detectSeparators)
		where T : IFloatingPoint<T>
	{
		// Guarded here, not just in ParseDetected, so every current and future path into Parse
		// with a possibly-empty span is covered. ParseDetected's separator-stripping normalization
		// can reduce input that was entirely repeated-separator noise (".." , ",,,") down to an
		// empty span; falling through to trimmed[^1] below would throw IndexOutOfRangeException
		// on that empty span. Malformed, not Empty -- the caller's original input wasn't empty, it
		// was fully consumed as separator noise, which is a grammar failure, not an absence.
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);

		if (detectSeparators)
			return ParseDetected<T>(trimmed);

		if (typeof(T) == typeof(decimal) && CountDigits(trimmed) > DecimalDigitGuard)
			return new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);

		// HyperCast.NumFormat has no currency-symbol concept at all (its own XML doc: "Currency
		// symbols ... are deliberately not supported"), yet RealStyles includes
		// AllowCurrencySymbol and the managed T.TryParse path genuinely honors a provider-declared
		// currency symbol. Routing to native for any CultureInfo provider -- including one whose
		// input actually carries a currency symbol -- would mis-route a well-formed managed-only
		// input into a native Malformed. Gate on invariance, exactly like IntegerParser.Parse does
		// for the identical reason: native is only sound when the caller's own provider carries no
		// currency/grouping/negative-notation conventions of its own to honor.
		if (NativeCapability.Available && provider is CultureInfo culture && IsInvariant(provider) &&
			TryParseNative<T>(trimmed, HyperCast.NumFormat.From(culture), out var nativeResult))
			return nativeResult;

		if (trimmed[^1] != '%')
		{
			return T.TryParse(trimmed, RealStyles, provider, out var value) ?
				(T.IsFinite(value) ? new Success<T>(value) : NonFiniteFailure<T>(trimmed, trimmed)) :
				ClassifyOverflow<T>(trimmed, trimmed, provider);
		}

		var body = trimmed[..^1].TrimEnd();
		return T.TryParse(body, RealStyles, provider, out var percent) ?
			(T.IsFinite(percent) ? new Success<T>(percent / T.CreateChecked(100)) : NonFiniteFailure<T>(body, trimmed)) :
			ClassifyOverflow<T>(body, trimmed, provider);
	}

	// decimal has no HyperCast native door, and decimal.TryParse simply returns false on overflow --
	// no infinity concept the way double/float have, so the IsFinite-based NonFiniteFailure branch
	// above never fires for decimal. A well-formed-but-too-large decimal literal (decimal.MaxValue+1,
	// "1e40") must still surface OutOfRange, not a bare Malformed collapse, matching this class's own
	// documented contract (see the remarks at the top of this file). Probe with double -- a strictly
	// wider finite range than decimal, under the SAME styles/provider decimal itself used -- to tell
	// "numerically well-formed, just too large for T" from "not a real number at all." Double/float
	// never reach this on a TryParse failure (their own overflow-to-infinity is already caught by the
	// IsFinite branch above, before this point, since T.TryParse succeeds and returns an infinite
	// value for them rather than failing outright), so this only ever changes decimal's classification.
	static Failure ClassifyOverflow<T>(ReadOnlySpan<char> body, ReadOnlySpan<char> trimmed, IFormatProvider provider)
		where T : IFloatingPoint<T> =>
		typeof(T) == typeof(decimal) && double.TryParse(body, RealStyles, provider, out var probe) && double.IsFinite(probe) ?
			new Failure(ParseFailure.OutOfRange, trimmed, typeof(T).Name) :
			new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);

	static bool IsInvariant(IFormatProvider provider) =>
		ReferenceEquals(NumberFormatInfo.GetInstance(provider), NumberFormatInfo.InvariantInfo);

	// The HyperCast.NumFormat.Detect profile is the native-side twin of ParseDetected below — same
	// structural rules, HyperCast's own implementation. Routed here first (native available, no
	// caller culture needed) so a detect-opted caller gets the native engine exactly like every
	// other caller does; ParseDetected is the managed fallback, not a second source of truth.
	static Result<T>? ParseDetectedNative<T>(ReadOnlySpan<char> trimmed)
		where T : IFloatingPoint<T> =>
		NativeCapability.Available && TryParseNative<T>(trimmed, HyperCast.NumFormat.Detect, out var nativeResult) ?
			nativeResult :
			null;

	static Result<T> ParseDetected<T>(ReadOnlySpan<char> trimmed)
		where T : IFloatingPoint<T>
	{
		if (ParseDetectedNative<T>(trimmed) is { } native)
			return native;

		if (!TryResolveSeparatorRoles(trimmed, out var decimalSeparator, out var groupSeparator))
			return new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);

		Span<char> normalized = trimmed.Length <= 128 ? stackalloc char[trimmed.Length] : new char[trimmed.Length];
		var length = 0;
		foreach (var character in trimmed)
		{
			if (groupSeparator is { } group && character == group)
				continue;
			normalized[length++] = decimalSeparator is { } decimalChar && character == decimalChar ? '.' : character;
		}

		return Parse<T>(normalized[..length], CultureInfo.InvariantCulture, detectSeparators: false);
	}

	// HyperCast's SeparatorDetect rules (structural, never a culture guess):
	//   - no '.'/',' present at all -> nothing to resolve, pass the text through unchanged.
	//   - both '.' and ',' present -> the rightmost occurrence is decimal, the other is grouping.
	//   - one distinct separator, repeated -> grouping only (no decimal portion at all).
	//   - one distinct separator, appearing once, with a right-of-separator digit run that is not
	//     exactly 3 digits -> that separator is decimal (grouping is always exactly 3 digits/group).
	//   - one distinct separator, appearing once, with exactly 3 digits to the right -> decimal only
	//     if the integer part is a bare zero (a leading-zero fraction, e.g. "0,785"); otherwise the
	//     3-vs-grouping shape is genuinely ambiguous with no culture to break the tie -> Malformed.
	static bool TryResolveSeparatorRoles(ReadOnlySpan<char> trimmed, out char? decimalSeparator, out char? groupSeparator)
	{
		decimalSeparator = null;
		groupSeparator = null;

		int dotCount = 0, commaCount = 0;
		int lastDotIndex = -1, lastCommaIndex = -1;
		for (var i = 0; i < trimmed.Length; i++)
		{
			switch (trimmed[i])
			{
				case '.':
					dotCount++;
					lastDotIndex = i;
					break;
				case ',':
					commaCount++;
					lastCommaIndex = i;
					break;
			}
		}

		if (dotCount == 0 && commaCount == 0)
			return true;

		if (dotCount > 0 && commaCount > 0)
		{
			if (lastDotIndex > lastCommaIndex)
			{
				decimalSeparator = '.';
				groupSeparator = ',';
			}
			else
			{
				decimalSeparator = ',';
				groupSeparator = '.';
			}
			return true;
		}

		var separator = dotCount > 0 ? '.' : ',';
		var count = dotCount > 0 ? dotCount : commaCount;
		var lastIndex = dotCount > 0 ? lastDotIndex : lastCommaIndex;

		if (count > 1)
		{
			groupSeparator = separator;
			return true;
		}

		var rightDigits = CountLeadingDigits(trimmed[(lastIndex + 1)..]);
		if (rightDigits != 3)
		{
			decimalSeparator = separator;
			return true;
		}

		if (IsZeroIntegerPart(trimmed[..lastIndex]))
		{
			decimalSeparator = separator;
			return true;
		}

		return false;
	}

	static int CountLeadingDigits(ReadOnlySpan<char> span)
	{
		var count = 0;
		while (count < span.Length && char.IsAsciiDigit(span[count]))
			count++;
		return count;
	}

	static bool IsZeroIntegerPart(ReadOnlySpan<char> leftOfSeparator)
	{
		var body = leftOfSeparator.TrimStart("+-(");
		return body.Length > 0 && body.IndexOfAnyExcept('0') < 0;
	}

	// A well-formed literal that overflows to a non-finite value carries at least one digit
	// character (1e400, 1e39); the genuinely non-numeric symbols (NaN, Infinity, -Infinity) carry
	// none. This is the same well-formed-but-out-of-range distinction HyperCast's own Cast.Single/
	// Cast.Double draw natively -- confirmed against the installed package's XML doc, not assumed.
	static Failure NonFiniteFailure<T>(ReadOnlySpan<char> numericBody, ReadOnlySpan<char> trimmed)
		where T : IFloatingPoint<T> =>
		CountDigits(numericBody) > 0 ?
			new Failure(ParseFailure.OutOfRange, trimmed, typeof(T).Name) :
			new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);

	static int CountDigits(ReadOnlySpan<char> span)
	{
		var count = 0;
		foreach (var character in span)
			if (char.IsAsciiDigit(character))
				count++;
		return count;
	}

	// Every real type this parser routes natively (double, float -- decimal has no HyperCast
	// native door and always stays managed) gets its own typeof(T) branch, JIT-eliminated to a
	// single true branch per concrete T, the same pattern IntegerParser's TryParseNative uses.
	static bool TryParseNative<T>(ReadOnlySpan<char> trimmed, HyperCast.NumFormat format, out Result<T> result)
		where T : IFloatingPoint<T>
	{
		if (typeof(T) == typeof(double))
		{
			result = TranslateReal<T, double>(HyperCast.Cast.Double(trimmed, format), trimmed);
			return true;
		}
		if (typeof(T) == typeof(float))
		{
			result = TranslateReal<T, float>(HyperCast.Cast.Single(trimmed, format), trimmed);
			return true;
		}
		result = default!;
		return false;
	}

	// Reason-based translation, never a bare collapse (Task 10's corrected shape): OutOfRange maps
	// explicitly, every other Fault reason maps to Malformed. TryGetValue, not a switch expression,
	// for the same reason IntegerParser.Translate uses it -- CS8780 when TNative is a generic type
	// parameter in a union pattern match.
	static Result<T> TranslateReal<T, TNative>(HyperCast.Verdict<TNative> verdict, ReadOnlySpan<char> trimmed)
		where T : IFloatingPoint<T>
		where TNative : struct, IFloatingPoint<TNative>
	{
		if (verdict.TryGetValue(out HyperCast.Success<TNative> success))
			return new Success<T>(T.CreateChecked(success.Value));

		verdict.TryGetValue(out HyperCast.Fault fault);
		return fault.Reason == HyperCast.CastFailure.OutOfRange ?
			new Failure(ParseFailure.OutOfRange, trimmed, typeof(T).Name) :
			new Failure(ParseFailure.Malformed, trimmed, typeof(T).Name);
	}
}
