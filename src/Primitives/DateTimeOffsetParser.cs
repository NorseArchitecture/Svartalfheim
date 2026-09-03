using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="DateTimeOffset"/>. The ISO door accepts the RFC 3339 grammar
/// <c>yyyy-MM-ddTHH:mm:ss[.f{1..9}](Z|±hh:mm)</c> — the date/time separator may be <c>T</c> or
/// <c>t</c>, the zone a literal <c>Z</c>/<c>z</c> or a numeric <c>±hh:mm</c> offset (colon
/// required; magnitude capped at 14 hours, matching <see cref="DateTimeOffset"/>'s own ceiling) —
/// normalized to UTC. A zone-less or space-separated form, a missing-seconds or missing-colon
/// offset, or a tenth-or-later fractional digit is <see cref="ParseFailure.Malformed"/> (the
/// eighth and ninth fractional digits truncate to ticks rather than round, matching HyperCast's
/// own native grammar). An instant outside <c>0001-01-01</c> to <c>9999-12-31</c> UTC — including
/// <c>0000-01-01</c>, whose year token is well-formed but unrepresentable, and an in-range local
/// date/time whose declared offset shifts the UTC-equivalent past either bound — is
/// <see cref="ParseFailure.OutOfRange"/>, never a bare <see cref="ParseFailure.Malformed"/>
/// collapse; the two boundary instants themselves (<see cref="DateTimeOffset.MinValue"/>/
/// <see cref="DateTimeOffset.MaxValue"/>) are ordinary successes, not rejected sentinels. The
/// exact door honors a single caller-declared format under a required provider, also resolving to
/// UTC (never local). <see cref="ParseUnix"/> reads a declared Unix epoch and, unlike the ISO
/// door, does reject <see cref="DateTimeOffset.MinValue"/>/<see cref="DateTimeOffset.MaxValue"/>
/// via its own sentinel guard.
/// </summary>
public static class DateTimeOffsetParser
{
	const string
		ExpectedType = nameof(DateTimeOffset),
		IsoLabel = "ISO 8601";

	const long
		MinUnixSeconds = -62135596800L,
		MaxUnixSeconds = 253402300799L,
		MinUnixMilliseconds = -62135596800000L,
		MaxUnixMilliseconds = 253402300799999L;

	// "yyyy-MM-ddTHH:mm:ss" -- the RFC 3339 date/time prefix before any fractional seconds or
	// zone designator, fixed-width so every offset ParseIsoManaged below indexes by is a
	// compile-time constant.
	const int LocalLength = 19;

	// DateTimeOffset's own offset ceiling (TimeSpan.FromHours(14)), in minutes so TryParseZone
	// never needs to construct a TimeSpan just to compare magnitudes.
	const int MaxOffsetMinutes = 14 * 60;

	const DateTimeStyles
		IsoStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
		ExactStyles = IsoStyles | DateTimeStyles.AllowWhiteSpaces;

	/// <summary>Parses an ISO/RFC 3339 datetime with a mandatory zone, normalized to UTC. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized, zone-less, or over-precise ⇒ <see cref="ParseFailure.Malformed"/>; outside <c>0001-01-01</c>..<c>9999-12-31</c> UTC ⇒ <see cref="ParseFailure.OutOfRange"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<DateTimeOffset> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseIso(trimmed);
	}

	/// <summary>Parses an optional ISO/RFC 3339 datetime. Empty ⇒ absent; unrecognized, zone-less, or over-precise ⇒ <see cref="ParseFailure.Malformed"/>; outside <c>0001-01-01</c>..<c>9999-12-31</c> UTC ⇒ <see cref="ParseFailure.OutOfRange"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<DateTimeOffset>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseIso(trimmed);
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
	public static Result<DateTimeOffset>? ParseExactOptional(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseExact(trimmed, format, provider);
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
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseUnixCore(trimmed, precision);
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
		return trimmed.IsEmpty ?
			null :
			ParseUnixCore(trimmed, precision);
	}

	static Result<DateTimeOffset> ParseIso(ReadOnlySpan<char> trimmed)
	{
		if (NativeCapability.Available)
			return HyperCast.Cast.Timestamp(trimmed) switch
			{
				HyperCast.Success<DateTimeOffset> s => new Success<DateTimeOffset>(s.Value),
				HyperCast.Fault { Reason: HyperCast.CastFailure.OutOfRange } => new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType, IsoLabel),
				HyperCast.Fault => new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel),
			};

		return ParseIsoManaged(trimmed);
	}

	// Hand-rolled RFC 3339 grammar -- DateTimeOffset.TryParse alone is both too lenient (it
	// accepts a space separator, a missing seconds field, and a colon-less numeric offset) and,
	// at the type's own upper boundary, too strict: rounding a 9-digit fractional second before
	// truncating to ticks overflows 9999-12-31T23:59:59.999999999Z past MaxValue even though
	// HyperCast's own documented grammar (and its corpus) accepts it. Every check below mirrors
	// that documented grammar directly, not the BCL's more permissive one.
	static Result<DateTimeOffset> ParseIsoManaged(ReadOnlySpan<char> trimmed)
	{
		if (trimmed.Length <= LocalLength ||
			trimmed[4] != '-' || trimmed[7] != '-' ||
			trimmed[10] is not ('T' or 't') ||
			trimmed[13] != ':' || trimmed[16] != ':')
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);

		// DateTime's own custom parser leniently accepts "24" as an hour when minutes and
		// seconds are both zero (rolling over to the next day's midnight) -- RFC 3339 and
		// HyperCast's corpus both reject it outright.
		if (trimmed[11] == '2' && trimmed[12] == '4')
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);

		// A "0000" year is a grammatically fine four-digit token, but the proleptic Gregorian
		// calendar .NET implements has no year zero -- unrepresentable, not unrecognized.
		if (trimmed[..4].SequenceEqual("0000"))
			return new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType, IsoLabel);

		var rest = trimmed[LocalLength..];
		var fractionTicks = 0;
		if (rest[0] == '.')
		{
			var digitsEnd = 1;
			while (digitsEnd < rest.Length && char.IsAsciiDigit(rest[digitsEnd]))
				digitsEnd++;
			var digits = rest[1..digitsEnd];
			// HyperCast's own grammar: one to nine fractional digits; the eighth and ninth
			// truncate to ticks, the tenth onward has no .NET representation and is rejected.
			if (digits.Length is 0 or > 9)
				return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);
			fractionTicks = TruncateToTicks(digits);
			rest = rest[digitsEnd..];
		}

		if (!TryParseZone(rest, out var offsetMinutes))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);

		// Normalize the 'T'/'t' separator so one case-sensitive exact format validates every
		// remaining calendar component (month 1-12, day-of-month against leap years, minute/
		// second range, no leap second) without a second format string for the lowercase form.
		Span<char> normalized = stackalloc char[LocalLength];
		trimmed[..LocalLength].CopyTo(normalized);
		normalized[10] = 'T';
		if (!DateTime.TryParseExact(normalized, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);

		if (fractionTicks > 0)
			local = local.AddTicks(fractionTicks);

		try
		{
			return new Success<DateTimeOffset>(new DateTimeOffset(local, TimeSpan.FromMinutes(offsetMinutes)));
		}
		catch (ArgumentOutOfRangeException)
		{
			// The offset magnitude was already bounded to +/-14:00 in TryParseZone -- the only
			// way the constructor still rejects this is a UTC-equivalent past 0001-01-01 or
			// 9999-12-31.
			return new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType, IsoLabel);
		}
	}

	// Recognizes "Z"/"z" (zero offset) or a "+hh:mm"/"-hh:mm" numeric offset -- colon required, so
	// a missing-colon form like "+0500" is rejected here rather than left for
	// DateTimeOffset.TryParse to accept leniently. Magnitude is capped at DateTimeOffset's own
	// +/-14:00 ceiling, so an out-of-magnitude offset (e.g. "+24:00") reads as Malformed grammar,
	// never an OutOfRange instant.
	static bool TryParseZone(ReadOnlySpan<char> rest, out int offsetMinutes)
	{
		if (rest.Length == 1 && rest[0] is 'Z' or 'z')
		{
			offsetMinutes = 0;
			return true;
		}

		if (rest.Length == 6 &&
			rest[0] is '+' or '-' &&
			char.IsAsciiDigit(rest[1]) && char.IsAsciiDigit(rest[2]) &&
			rest[3] == ':' &&
			char.IsAsciiDigit(rest[4]) && char.IsAsciiDigit(rest[5]))
		{
			var hours = (rest[1] - '0') * 10 + (rest[2] - '0');
			var minutes = (rest[4] - '0') * 10 + (rest[5] - '0');
			var total = hours * 60 + minutes;
			if (minutes <= 59 && total <= MaxOffsetMinutes)
			{
				offsetMinutes = rest[0] == '+' ? total : -total;
				return true;
			}
		}

		offsetMinutes = 0;
		return false;
	}

	// Ticks are 100ns (seven decimal digits of a second); a declared fraction beyond that
	// truncates -- never rounds, which is what keeps 9999-12-31T23:59:59.999999999Z from
	// overflowing past DateTimeOffset.MaxValue instead of resolving to it.
	static int TruncateToTicks(ReadOnlySpan<char> digits)
	{
		Span<char> padded = stackalloc char[7];
		for (var i = 0; i < padded.Length; i++)
			padded[i] = i < digits.Length ? digits[i] : '0';
		return int.Parse(padded, CultureInfo.InvariantCulture);
	}

	static Result<DateTimeOffset> ParseExact(ReadOnlySpan<char> trimmed, string format, IFormatProvider provider) =>
		DateTimeOffset.TryParseExact(trimmed, format, provider, ExactStyles, out var value) &&
		!IsSentinel(value) ?
			new Success<DateTimeOffset>(value) :
			new Failure(ParseFailure.Malformed, trimmed, ExpectedType, format);

	static Result<DateTimeOffset> ParseUnixCore(ReadOnlySpan<char> trimmed, UnixPrecision precision)
	{
		if (!long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var epoch) ||
			!InRange(epoch, precision))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType);
		var value = precision == UnixPrecision.Seconds ?
			DateTimeOffset.FromUnixTimeSeconds(epoch) :
			DateTimeOffset.FromUnixTimeMilliseconds(epoch);
		return IsSentinel(value) ?
			new Failure(ParseFailure.Malformed, trimmed, ExpectedType) :
			new Success<DateTimeOffset>(value);
	}

	static bool InRange(long epoch, UnixPrecision precision) =>
		precision == UnixPrecision.Seconds ?
			epoch is >= MinUnixSeconds and <= MaxUnixSeconds :
			epoch is >= MinUnixMilliseconds and <= MaxUnixMilliseconds;

	static bool IsSentinel(DateTimeOffset value) =>
		value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue;

	static void GuardPrecision(UnixPrecision precision)
	{
		if (precision is not (UnixPrecision.Seconds or UnixPrecision.Milliseconds))
			throw new ArgumentOutOfRangeException(nameof(precision), precision, "Precision must be Seconds or Milliseconds.");
	}
}
