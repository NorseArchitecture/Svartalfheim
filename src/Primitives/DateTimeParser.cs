using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="DateTime"/>. Identical ISO profile to
/// <see cref="DateTimeOffsetParser"/> — mandatory zone, normalized to UTC (<see cref="DateTimeKind.Utc"/>) —
/// plus a declared-exact door (carrying <see cref="DateTimeStyles.NoCurrentDateDefault"/> so a missing
/// date component fails loud rather than defaulting to today) and a declared <see cref="ParseUnix"/>
/// epoch door. The sentinel guard rejects <see cref="DateTime.MinValue"/>/<see cref="DateTime.MaxValue"/>.
/// </summary>
public static class DateTimeParser
{
	const string
		ExpectedType = nameof(DateTime),
		IsoLabel = "ISO 8601";

	const long
		MinUnixSeconds = -62135596800L,
		MaxUnixSeconds = 253402300799L,
		MinUnixMilliseconds = -62135596800000L,
		MaxUnixMilliseconds = 253402300799999L;

	static readonly string[] _isoFormats =
	[
		"yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'",
		"yyyy-MM-ddTHH:mm:ss'Z'",
		"yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz",
		"yyyy-MM-ddTHH:mm:sszzz",
	];

	const DateTimeStyles
		IsoStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
		ExactStyles = IsoStyles | DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault;

	/// <summary>Parses an ISO datetime with a mandatory zone to a UTC <see cref="DateTime"/>. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized, zone-less, or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<DateTime> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseIso(trimmed);
	}

	/// <summary>Parses an optional ISO datetime. Empty ⇒ absent; unrecognized, zone-less, or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<DateTime>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseIso(trimmed);
	}

	/// <summary>Parses a datetime against a single caller-declared <paramref name="format"/>, resolving to UTC (never local), with no current-date default.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<DateTime> ParseExactRequired(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseExact(trimmed, format, provider);
	}

	/// <summary>Parses an optional datetime against a single caller-declared <paramref name="format"/>.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<DateTime>? ParseExactOptional(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseExact(trimmed, format, provider);
	}

	/// <summary>Parses a declared Unix epoch to a UTC <see cref="DateTime"/> (integer; negatives allowed). Empty ⇒ <see cref="ParseFailure.Empty"/>; non-integer, out-of-range, or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="precision">The declared unit. Must be <see cref="UnixPrecision.Seconds"/> or <see cref="UnixPrecision.Milliseconds"/>.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="precision"/> is undefined.</exception>
	public static Result<DateTime> ParseUnix(ReadOnlySpan<char> input, UnixPrecision precision)
	{
		GuardPrecision(precision);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseUnixCore(trimmed, precision);
	}

	/// <summary>Parses an optional declared Unix epoch to a UTC <see cref="DateTime"/>. Empty ⇒ absent; non-integer, out-of-range, or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <param name="precision">The declared unit. Must be <see cref="UnixPrecision.Seconds"/> or <see cref="UnixPrecision.Milliseconds"/>.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="precision"/> is undefined.</exception>
	public static Result<DateTime>? ParseUnixOptional(ReadOnlySpan<char> input, UnixPrecision precision)
	{
		GuardPrecision(precision);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseUnixCore(trimmed, precision);
	}

	static Result<DateTime> ParseIso(ReadOnlySpan<char> trimmed) =>
		DateTime.TryParseExact(trimmed, _isoFormats, CultureInfo.InvariantCulture, IsoStyles, out var value) && !IsSentinel(value)
			? new Success<DateTime>(value)
			: new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);

	static Result<DateTime> ParseExact(ReadOnlySpan<char> trimmed, string format, IFormatProvider provider)
	{
		if (DateTime.TryParseExact(trimmed, format, provider, ExactStyles, out var value)
			&& !IsSentinel(value))
			return new Success<DateTime>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, format);
	}

	static Result<DateTime> ParseUnixCore(ReadOnlySpan<char> trimmed, UnixPrecision precision)
	{
		if (!long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var epoch)
			|| !InRange(epoch, precision))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType);
		var value = (precision == UnixPrecision.Seconds
			? DateTimeOffset.FromUnixTimeSeconds(epoch)
			: DateTimeOffset.FromUnixTimeMilliseconds(epoch)).UtcDateTime;
		if (IsSentinel(value))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType);
		return new Success<DateTime>(value);
	}

	static bool InRange(long epoch, UnixPrecision precision) =>
		precision == UnixPrecision.Seconds
			? epoch is >= MinUnixSeconds and <= MaxUnixSeconds
			: epoch is >= MinUnixMilliseconds and <= MaxUnixMilliseconds;

	static bool IsSentinel(DateTime value) =>
		value == DateTime.MinValue || value == DateTime.MaxValue;

	static void GuardPrecision(UnixPrecision precision)
	{
		if (precision is not (UnixPrecision.Seconds or UnixPrecision.Milliseconds))
			throw new ArgumentOutOfRangeException(nameof(precision), precision, "Precision must be Seconds or Milliseconds.");
	}
}
