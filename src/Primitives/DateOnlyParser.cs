using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="DateOnly"/>. The ISO door accepts exactly
/// <c>yyyy-MM-dd</c> under <see cref="CultureInfo.InvariantCulture"/> — a well-formed but
/// unrepresentable <c>0000</c> year is <see cref="ParseFailure.OutOfRange"/>, never a bare
/// <see cref="ParseFailure.Malformed"/> collapse; <see cref="DateOnly.MinValue"/>
/// (<c>0001-01-01</c>) and <see cref="DateOnly.MaxValue"/> (<c>9999-12-31</c>) are ordinary
/// successes on this door, matching HyperCast's own corpus (<c>date.json</c>) — see the
/// 2026-09-03 amendment to the temporal-parsers design spec §9. The exact door accepts a single
/// caller-declared format under a required provider and still carries the original sentinel
/// guard (unaudited against HyperCast so far — no corpus file covers it yet). Culture-insensitive
/// on the ISO door (no provider — ISO 8601 is invariant).
/// </summary>
public static class DateOnlyParser
{
	const string
		ExpectedType = nameof(DateOnly),
		IsoFormat = "yyyy-MM-dd",
		IsoLabel = "ISO 8601";

	/// <summary>Parses an ISO <c>yyyy-MM-dd</c> date. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized ⇒ <see cref="ParseFailure.Malformed"/>; a well-formed but unrepresentable <c>0000</c> year ⇒ <see cref="ParseFailure.OutOfRange"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<DateOnly> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseIso(trimmed);
	}

	/// <summary>Parses an optional ISO date. Empty ⇒ absent (<see langword="null"/>); unrecognized ⇒ <see cref="ParseFailure.Malformed"/>; a well-formed but unrepresentable <c>0000</c> year ⇒ <see cref="ParseFailure.OutOfRange"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<DateOnly>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseIso(trimmed);
	}

	/// <summary>Parses a date against a single caller-declared <paramref name="format"/>.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<DateOnly> ParseExactRequired(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseExact(trimmed, format, provider);
	}

	/// <summary>Parses an optional date against a single caller-declared <paramref name="format"/>.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<DateOnly>? ParseExactOptional(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseExact(trimmed, format, provider);
	}

	static Result<DateOnly> ParseIso(ReadOnlySpan<char> trimmed)
	{
		if (NativeCapability.Available)
			return HyperCast.Cast.Date(trimmed) switch
			{
				HyperCast.Success<DateOnly> s => new Success<DateOnly>(s.Value),
				HyperCast.Fault { Reason: HyperCast.CastFailure.OutOfRange } => new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType, IsoLabel),
				HyperCast.Fault => new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel),
			};

		return ParseIsoManaged(trimmed);
	}

	// Hand-rolled ahead of the BCL call for one reason only: DateOnly.TryParseExact fails
	// year "0000" outright (no year zero in the proleptic Gregorian calendar), collapsing a
	// well-formed-but-unrepresentable token into the same false as any other garbage. HyperCast's
	// corpus distinguishes the two (OutOfRange vs Malformed), so the check runs first. Unlike
	// DateTimeOffsetParser's RFC 3339 rewrite, no other divergence exists here — TryParseExact's
	// own calendar validation (month 1-12, day-of-month against leap years) already agrees with
	// the corpus on every other vector, so the exact-format call still does the rest of the work.
	static Result<DateOnly> ParseIsoManaged(ReadOnlySpan<char> trimmed)
	{
		// Structural shape first (fixed length, dashes at the "yyyy-MM-dd" positions) -- only a
		// genuinely well-formed "0000-MM-dd" token gets the OutOfRange verdict; a wrong-separator
		// or otherwise garbled string with a leading "0000" (e.g. "0000/01/01") stays Malformed.
		if (trimmed.Length == IsoFormat.Length && trimmed[4] == '-' && trimmed[7] == '-' &&
			trimmed[..4].SequenceEqual("0000"))
			return new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType, IsoLabel);

		return DateOnly.TryParseExact(trimmed, IsoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ?
			new Success<DateOnly>(value) :
			new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);
	}

	static Result<DateOnly> ParseExact(ReadOnlySpan<char> trimmed, string format, IFormatProvider provider)
	{
		if (DateOnly.TryParseExact(trimmed, format, provider, DateTimeStyles.AllowWhiteSpaces, out var value) &&
			!IsSentinel(value))
			return new Success<DateOnly>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, format);
	}

	static bool IsSentinel(DateOnly value) =>
		value == DateOnly.MinValue || value == DateOnly.MaxValue;
}
