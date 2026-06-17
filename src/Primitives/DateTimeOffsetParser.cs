using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="DateTimeOffset"/>. The ISO door accepts the
/// <c>yyyy-MM-ddTHH:mm:ss[.fffffff]</c> profile with a <b>mandatory</b> zone (literal <c>Z</c> or a
/// numeric <c>±hh:mm</c> offset), normalized to UTC; a zone-less or space-separated form is
/// <see cref="ParseFailure.Malformed"/>. The exact door honors a single caller-declared format
/// under a required provider, also resolving to UTC (never local). <see cref="ParseUnix"/> reads a
/// declared Unix epoch. The sentinel guard rejects <see cref="DateTimeOffset.MinValue"/>/<see cref="DateTimeOffset.MaxValue"/>.
/// </summary>
public static class DateTimeOffsetParser
{
	const string ExpectedType = nameof(DateTimeOffset);
	const string IsoLabel = "ISO 8601";

	const long MinUnixSeconds = -62135596800L;
	const long MaxUnixSeconds = 253402300799L;
	const long MinUnixMilliseconds = -62135596800000L;
	const long MaxUnixMilliseconds = 253402300799999L;

	static readonly string[] _isoFormats =
	[
		"yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'",
		"yyyy-MM-ddTHH:mm:ss'Z'",
		"yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz",
		"yyyy-MM-ddTHH:mm:sszzz",
	];

	const DateTimeStyles IsoStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
	const DateTimeStyles ExactStyles = IsoStyles | DateTimeStyles.AllowWhiteSpaces;

	/// <summary>Parses an ISO datetime with a mandatory zone, normalized to UTC. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized, zone-less, or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<DateTimeOffset> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return ParseIso(trimmed);
	}

	/// <summary>Parses an optional ISO datetime. Empty ⇒ absent; unrecognized, zone-less, or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<DateTimeOffset>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return ParseIso(trimmed);
	}

	/// <summary>Parses a datetime against a single caller-declared <paramref name="format"/>, resolving to UTC (never local).</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<DateTimeOffset> ParseExactRequired(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return ParseExact(trimmed, format, provider);
	}

	/// <summary>Parses an optional datetime against a single caller-declared <paramref name="format"/>.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<DateTimeOffset>? ParseExactOptional(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return ParseExact(trimmed, format, provider);
	}

	/// <summary>Parses a declared Unix epoch (integer; negatives allowed). Empty ⇒ <see cref="ParseFailure.Empty"/>; non-integer, out-of-range, or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="precision">The declared unit. Must be <see cref="UnixPrecision.Seconds"/> or <see cref="UnixPrecision.Milliseconds"/>.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="precision"/> is undefined.</exception>
	public static Result<DateTimeOffset> ParseUnix(ReadOnlySpan<char> input, UnixPrecision precision)
	{
		GuardPrecision(precision);
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return ParseUnixCore(trimmed, precision);
	}

	/// <summary>Parses an optional declared Unix epoch. Empty ⇒ absent; non-integer, out-of-range, or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="precision">The declared unit. Must be <see cref="UnixPrecision.Seconds"/> or <see cref="UnixPrecision.Milliseconds"/>.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="precision"/> is undefined.</exception>
	public static Result<DateTimeOffset>? ParseUnixOptional(ReadOnlySpan<char> input, UnixPrecision precision)
	{
		GuardPrecision(precision);
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return ParseUnixCore(trimmed, precision);
	}

	static Result<DateTimeOffset> ParseIso(ReadOnlySpan<char> trimmed)
	{
		if (DateTimeOffset.TryParseExact(trimmed, _isoFormats, CultureInfo.InvariantCulture, IsoStyles, out var value)
			&& !IsSentinel(value))
			return new Success<DateTimeOffset>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);
	}

	static Result<DateTimeOffset> ParseExact(ReadOnlySpan<char> trimmed, string format, IFormatProvider provider)
	{
		if (DateTimeOffset.TryParseExact(trimmed, format, provider, ExactStyles, out var value)
			&& !IsSentinel(value))
			return new Success<DateTimeOffset>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, format);
	}

	static Result<DateTimeOffset> ParseUnixCore(ReadOnlySpan<char> trimmed, UnixPrecision precision)
	{
		if (!long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var epoch)
			|| !InRange(epoch, precision))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType);
		var value = precision == UnixPrecision.Seconds
			? DateTimeOffset.FromUnixTimeSeconds(epoch)
			: DateTimeOffset.FromUnixTimeMilliseconds(epoch);
		if (IsSentinel(value))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType);
		return new Success<DateTimeOffset>(value);
	}

	static bool InRange(long epoch, UnixPrecision precision) =>
		precision == UnixPrecision.Seconds
			? epoch is >= MinUnixSeconds and <= MaxUnixSeconds
			: epoch is >= MinUnixMilliseconds and <= MaxUnixMilliseconds;

	static bool IsSentinel(DateTimeOffset value) =>
		value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue;

	static void GuardPrecision(UnixPrecision precision)
	{
		if (precision is not (UnixPrecision.Seconds or UnixPrecision.Milliseconds))
			throw new ArgumentOutOfRangeException(nameof(precision), precision, "Precision must be Seconds or Milliseconds.");
	}
}
