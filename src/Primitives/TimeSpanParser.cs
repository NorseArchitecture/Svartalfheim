using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="TimeSpan"/>. The no-format door accepts both the BCL colon form
/// (<c>[-][d.]hh:mm:ss[.fffffff]</c>, parsed under <see cref="CultureInfo.InvariantCulture"/>) and an
/// ISO-8601 duration (<c>PT1H30M</c>, <c>P3DT4H</c>, weeks) restricted to fixed components — year and
/// month (<c>P1Y</c>, <c>P2M</c>) are not fixed durations and are <see cref="ParseFailure.Malformed"/>.
/// The exact door honors <see cref="TimeSpan.TryParseExact(System.ReadOnlySpan{char}, System.ReadOnlySpan{char}, System.IFormatProvider, out System.TimeSpan)"/>.
/// The sentinel guard rejects <see cref="TimeSpan.MinValue"/>/<see cref="TimeSpan.MaxValue"/>; <see cref="TimeSpan.Zero"/> is valid.
/// </summary>
public static class TimeSpanParser
{
	const string ExpectedType = nameof(TimeSpan);
	const string IsoLabel = "ISO 8601";
	// Parse-sanity bound on digit runs — NOT the overflow guard; overflow is handled by TryAddTicks and the seconds bounds check.
	const int MaxDigits = 18;

	/// <summary>Parses a colon-form or ISO-8601-duration span. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<TimeSpan> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return ParseDuration(trimmed);
	}

	/// <summary>Parses an optional span. Empty ⇒ absent; unrecognized or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<TimeSpan>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return ParseDuration(trimmed);
	}

	/// <summary>Parses a span against a single caller-declared <paramref name="format"/>.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<TimeSpan> ParseExactRequired(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return ParseExact(trimmed, format, provider);
	}

	/// <summary>Parses an optional span against a single caller-declared <paramref name="format"/>.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<TimeSpan>? ParseExactOptional(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return ParseExact(trimmed, format, provider);
	}

	static Result<TimeSpan> ParseDuration(ReadOnlySpan<char> trimmed)
	{
		// A leading 'P' (optionally signed) is the ISO-8601 duration discriminator — colon form never
		// carries one, so the two grammars partition cleanly. Sniff first and route: feeding an ISO
		// duration to the BCL colon parser only to watch it fail costs 424 B per call (measured), an
		// allocation the colon parser charges on its reject path. The sniff sidesteps it.
		if (IsIsoDuration(trimmed))
		{
			if (TryParseIso8601Duration(trimmed, out var iso) && !IsSentinel(iso))
				return new Success<TimeSpan>(iso);
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);
		}
		if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var colon) && !IsSentinel(colon))
			return new Success<TimeSpan>(colon);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);
	}

	static bool IsIsoDuration(ReadOnlySpan<char> trimmed)
	{
		var index = trimmed.Length > 0 && trimmed[0] == '-' ? 1 : 0;
		return index < trimmed.Length && trimmed[index] is 'P' or 'p';
	}

	static Result<TimeSpan> ParseExact(ReadOnlySpan<char> trimmed, string format, IFormatProvider provider)
	{
		if (TimeSpan.TryParseExact(trimmed, format, provider, out var value) && !IsSentinel(value))
			return new Success<TimeSpan>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, format);
	}

	// Grammar: [-] 'P' { n('W'|'D') } [ 'T' { n('H'|'M') | n[.n]('S') } ] — at least one component;
	// year/month and any misplaced unit are rejected.
	static bool TryParseIso8601Duration(ReadOnlySpan<char> span, out TimeSpan result)
	{
		result = TimeSpan.Zero;
		var index = 0;
		var negative = false;
		if (index < span.Length && span[index] == '-')
		{
			negative = true;
			index++;
		}
		if (index >= span.Length || span[index] is not ('P' or 'p'))
			return false;
		index++;

		long ticks = 0;
		var inTime = false;
		var sawDateComponent = false;
		var sawTimeComponent = false;
		while (index < span.Length)
		{
			if (span[index] is 'T' or 't')
			{
				if (inTime)
					return false;
				inTime = true;
				index++;
				continue;
			}

			var start = index;
			while (index < span.Length && char.IsAsciiDigit(span[index]))
				index++;
			var hasFraction = false;
			if (index < span.Length && span[index] == '.')
			{
				hasFraction = true;
				index++;
				while (index < span.Length && char.IsAsciiDigit(span[index]))
					index++;
			}
			if (index == start || index - start > MaxDigits || index >= span.Length)
				return false;

			var number = span[start..index];
			var unit = span[index];
			index++;
			switch (unit)
			{
				case 'W' or 'w' when !inTime:
					if (hasFraction || !long.TryParse(number, out var weeks) || !TryAddTicks(ref ticks, weeks, 7 * TimeSpan.TicksPerDay))
						return false;
					sawDateComponent = true;
					break;
				case 'D' or 'd' when !inTime:
					if (hasFraction || !long.TryParse(number, out var days) || !TryAddTicks(ref ticks, days, TimeSpan.TicksPerDay))
						return false;
					sawDateComponent = true;
					break;
				case 'H' or 'h' when inTime:
					if (hasFraction || !long.TryParse(number, out var hours) || !TryAddTicks(ref ticks, hours, TimeSpan.TicksPerHour))
						return false;
					sawTimeComponent = true;
					break;
				case 'M' when inTime:
					if (hasFraction || !long.TryParse(number, out var minutes) || !TryAddTicks(ref ticks, minutes, TimeSpan.TicksPerMinute))
						return false;
					sawTimeComponent = true;
					break;
				case 'S' or 's' when inTime:
					if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds))
						return false;
					var secondTicks = seconds * TimeSpan.TicksPerSecond;
					if (secondTicks > long.MaxValue - ticks)
						return false;
					ticks += (long)secondTicks;
					sawTimeComponent = true;
					break;
				default:
					return false; // Y, M-before-T (months), or a misplaced unit
			}
		}

		if (!sawDateComponent && !sawTimeComponent)
			return false;
		if (inTime && !sawTimeComponent)
			return false; // a 'T' with no time component

		result = negative ? new TimeSpan(-ticks) : new TimeSpan(ticks);
		return true;
	}

	static bool IsSentinel(TimeSpan value) =>
		value == TimeSpan.MinValue || value == TimeSpan.MaxValue;

	// quantity and ticks are non-negative here; the sign is applied once at the end.
	static bool TryAddTicks(ref long ticks, long quantity, long ticksPerUnit)
	{
		if (quantity > (long.MaxValue - ticks) / ticksPerUnit)
			return false;
		ticks += quantity * ticksPerUnit;
		return true;
	}
}
