using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="TimeSpan"/>. The no-format door routes to HyperCast's native
/// <c>Duration</c> cast when available, and otherwise hand-rolls the identical grammar: three
/// cleanly-partitioned shapes — an ISO 8601 duration restricted to fixed components (<c>PT1H30M</c>,
/// <c>P3DT4H</c>, weeks; year and month are not fixed durations and are
/// <see cref="ParseFailure.Malformed"/>), the invariant colon form (<c>[-][d.]hh:mm[:ss[.f]]</c> —
/// <c>hh</c>/<c>mm</c>/<c>ss</c> are each exactly one or two digits and <c>hh</c> is always bounded
/// 0-23, whether or not a day prefix is present; a bare digit run with no colon, day prefix, or
/// <c>s</c> suffix is unrecognized), and protobuf JSON seconds (<c>3.5s</c>, case-insensitive
/// suffix). A fractional-seconds tail on the colon or seconds form, and the ISO form's own
/// <c>S</c> component, accept either <c>.</c> or <c>,</c> as the decimal mark and one to nine
/// digits — the eighth and ninth truncate to ticks (100ns) rather than round, matching HyperCast's
/// own documented precision. A magnitude beyond ±10,000 years (protobuf Duration's own ceiling,
/// ±315,576,000,000 whole seconds) is <see cref="ParseFailure.OutOfRange"/>, including
/// <see cref="TimeSpan.MinValue"/>/<see cref="TimeSpan.MaxValue"/> themselves, both of which sit
/// far outside that ceiling — there is no separate sentinel guard; the magnitude check alone
/// excludes them. The exact door honors
/// <see cref="TimeSpan.TryParseExact(System.ReadOnlySpan{char}, System.ReadOnlySpan{char}, System.IFormatProvider, out System.TimeSpan)"/>
/// verbatim, including its own leniency, and is never native-routed (HyperCast's duration door
/// takes no format string).
/// </summary>
public static class TimeSpanParser
{
	const string
		ExpectedType = nameof(TimeSpan),
		IsoLabel = "ISO 8601";
	// Parse-sanity bound on a single component's whole-digit run -- not the ±10,000-year cap
	// itself (that's MaxTicksMagnitude, checked once the value is fully accumulated). A run past
	// this length is rejected outright as Malformed, never as OutOfRange, matching HyperCast: a
	// 20-digit seconds count is Malformed, but an 18-digit one that overflows the cap is
	// OutOfRange.
	const int MaxDigits = 18;

	// protobuf Duration's own ceiling: ±10,000 years, expressed as whole seconds. A value is in
	// range while its magnitude, in ticks, stays under the tick-count of one whole second past
	// that ceiling -- so 315576000000.9999999s (still short of second 315576000001) is in range,
	// but 315576000001.0000000s is not. Verified against the native HyperCast.Managed 0.2.0
	// binary directly (not just the vendored corpus, which only exercises the whole-second
	// boundary) -- see the task report for the probe transcript.
	const long MaxCapSeconds = 315_576_000_000L;
	static readonly Int128 _maxTicksMagnitude = ((Int128)(MaxCapSeconds + 1) * TimeSpan.TicksPerSecond) - 1;

	/// <summary>Parses a colon-form, ISO-8601-duration, or protobuf-seconds span. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized ⇒ <see cref="ParseFailure.Malformed"/>; beyond ±10,000 years ⇒ <see cref="ParseFailure.OutOfRange"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<TimeSpan> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseDuration(trimmed);
	}

	/// <summary>Parses an optional span. Empty ⇒ absent; unrecognized ⇒ <see cref="ParseFailure.Malformed"/>; beyond ±10,000 years ⇒ <see cref="ParseFailure.OutOfRange"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<TimeSpan>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseDuration(trimmed);
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
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseExact(trimmed, format, provider);
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
		return trimmed.IsEmpty ?
			null :
			ParseExact(trimmed, format, provider);
	}

	static Result<TimeSpan> ParseDuration(ReadOnlySpan<char> trimmed)
	{
		if (NativeCapability.Available)
			return HyperCast.Cast.Duration(trimmed) switch
			{
				HyperCast.Success<TimeSpan> s => new Success<TimeSpan>(s.Value),
				HyperCast.Fault { Reason: HyperCast.CastFailure.OutOfRange } => new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType),
				HyperCast.Fault => new Failure(ParseFailure.Malformed, trimmed, ExpectedType),
			};

		if (IsIsoDuration(trimmed))
			return ToResult(TryParseIso8601Duration(trimmed, out var iso), iso, trimmed, IsoLabel);
		if (IsSecondsForm(trimmed))
			return ToResult(TryParseSecondsForm(trimmed, out var seconds), seconds, trimmed, null);
		return ToResult(TryParseColonForm(trimmed, out var colon), colon, trimmed, null);
	}

	static Result<TimeSpan> ToResult(DurationOutcome outcome, TimeSpan value, ReadOnlySpan<char> trimmed, string? label) =>
		outcome switch
		{
			DurationOutcome.Ok => new Success<TimeSpan>(value),
			DurationOutcome.OutOfRange => new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType, label),
			_ => new Failure(ParseFailure.Malformed, trimmed, ExpectedType, label),
		};

	static bool IsIsoDuration(ReadOnlySpan<char> trimmed)
	{
		var index = trimmed.Length > 0 && trimmed[0] == '-' ? 1 : 0;
		return index < trimmed.Length && trimmed[index] is 'P' or 'p';
	}

	// Protobuf JSON seconds ("3.5s") never collides with the colon form (no ':', '.', or ','
	// appears as the terminal character there) or the ISO form (checked first, so a "...S" ISO
	// duration never reaches this sniff) -- a trailing case-insensitive 's' partitions cleanly.
	static bool IsSecondsForm(ReadOnlySpan<char> trimmed) =>
		trimmed.Length > 0 && trimmed[^1] is 's' or 'S';

	static Result<TimeSpan> ParseExact(ReadOnlySpan<char> trimmed, string format, IFormatProvider provider) =>
		TimeSpan.TryParseExact(trimmed, format, provider, out var value) && !IsSentinel(value) ?
			new Success<TimeSpan>(value) :
			new Failure(ParseFailure.Malformed, trimmed, ExpectedType, format);

	static bool IsSentinel(TimeSpan value) =>
		value == TimeSpan.MinValue || value == TimeSpan.MaxValue;

	// Grammar: [-] 'P' { n('W'|'D') } [ 'T' { n('H'|'M') | n[.,n]('S') } ] — at least one
	// component; year/month and any misplaced unit are rejected. Accumulates in Int128 so a
	// component with the maximum allowed digit run never silently wraps before the magnitude
	// check below gets to render its own verdict.
	static DurationOutcome TryParseIso8601Duration(ReadOnlySpan<char> span, out TimeSpan result)
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
			return DurationOutcome.Malformed;
		index++;

		Int128 ticks = 0;
		var inTime = false;
		var sawDateComponent = false;
		var sawTimeComponent = false;
		while (index < span.Length)
		{
			if (span[index] is 'T' or 't')
			{
				if (inTime)
					return DurationOutcome.Malformed;
				inTime = true;
				index++;
				continue;
			}

			var start = index;
			while (index < span.Length && char.IsAsciiDigit(span[index]))
				index++;
			var intLen = index - start;

			var hasFraction = false;
			var fracStart = index;
			if (index < span.Length && span[index] is '.' or ',')
			{
				hasFraction = true;
				index++;
				fracStart = index;
				while (index < span.Length && char.IsAsciiDigit(span[index]))
					index++;
			}
			var fracLen = index - fracStart;

			if (intLen is 0 || intLen > MaxDigits || (hasFraction && fracLen is 0 or > 9) || index >= span.Length)
				return DurationOutcome.Malformed;

			var whole = span[start..(start + intLen)];
			var unit = span[index];
			index++;
			switch (unit)
			{
				case 'W' or 'w' when !inTime:
					if (hasFraction || !Int128.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var weeks))
						return DurationOutcome.Malformed;
					ticks += weeks * 7 * TimeSpan.TicksPerDay;
					sawDateComponent = true;
					break;
				case 'D' or 'd' when !inTime:
					if (hasFraction || !Int128.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var days))
						return DurationOutcome.Malformed;
					ticks += days * TimeSpan.TicksPerDay;
					sawDateComponent = true;
					break;
				case 'H' or 'h' when inTime:
					if (hasFraction || !Int128.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var hours))
						return DurationOutcome.Malformed;
					ticks += hours * TimeSpan.TicksPerHour;
					sawTimeComponent = true;
					break;
				case 'M' when inTime:
					if (hasFraction || !Int128.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
						return DurationOutcome.Malformed;
					ticks += minutes * TimeSpan.TicksPerMinute;
					sawTimeComponent = true;
					break;
				case 'S' or 's' when inTime:
					if (!Int128.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
						return DurationOutcome.Malformed;
					ticks += seconds * TimeSpan.TicksPerSecond;
					if (hasFraction)
						ticks += TruncateFractionToTicks(span[fracStart..(fracStart + fracLen)]);
					sawTimeComponent = true;
					break;
				default:
					return DurationOutcome.Malformed; // Y, M-before-T (months), or a misplaced unit
			}
		}

		if (!sawDateComponent && !sawTimeComponent)
			return DurationOutcome.Malformed;
		if (inTime && !sawTimeComponent)
			return DurationOutcome.Malformed; // a 'T' with no time component

		return Finish(negative, ticks, out result);
	}

	// Grammar: [-] digits [ ('.'|',') 1-9 digits ] ('s'|'S'), exactly. The integer part shares
	// the ISO form's MaxDigits sanity bound; the fraction shares the colon form's 1-9-digit,
	// truncate-past-ticks rule.
	static DurationOutcome TryParseSecondsForm(ReadOnlySpan<char> span, out TimeSpan result)
	{
		result = TimeSpan.Zero;
		var index = 0;
		var negative = false;
		if (index < span.Length && span[index] == '-')
		{
			negative = true;
			index++;
		}

		var start = index;
		while (index < span.Length && char.IsAsciiDigit(span[index]))
			index++;
		var intLen = index - start;
		if (intLen is 0 || intLen > MaxDigits)
			return DurationOutcome.Malformed;
		var whole = span[start..index];

		Int128 fractionTicks = 0;
		if (index < span.Length && span[index] is '.' or ',')
		{
			index++;
			var fracStart = index;
			while (index < span.Length && char.IsAsciiDigit(span[index]))
				index++;
			var fracLen = index - fracStart;
			if (fracLen is 0 or > 9)
				return DurationOutcome.Malformed;
			fractionTicks = TruncateFractionToTicks(span[fracStart..index]);
		}

		if (index != span.Length - 1 || span[index] is not ('s' or 'S'))
			return DurationOutcome.Malformed;
		if (!Int128.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
			return DurationOutcome.Malformed;

		return Finish(negative, (seconds * TimeSpan.TicksPerSecond) + fractionTicks, out result);
	}

	// Grammar: [-] [ digits '.' ] hh ':' mm [ ':' ss [ ('.'|',') 1-9 digits ] ] — hh/mm/ss are
	// each exactly one or two digits; hh is bounded 0-23 whether or not the day prefix is
	// present, mm and ss are each bounded 0-59. The day prefix's digit run, unlike hh/mm/ss,
	// carries no width limit beyond MaxDigits -- matching the ISO form's own week/day components.
	static DurationOutcome TryParseColonForm(ReadOnlySpan<char> span, out TimeSpan result)
	{
		result = TimeSpan.Zero;
		var index = 0;
		var negative = false;
		if (index < span.Length && span[index] == '-')
		{
			negative = true;
			index++;
		}

		if (!TryReadDigits(span, ref index, 1, MaxDigits, out var first))
			return DurationOutcome.Malformed;

		Int128 days = 0;
		var hasDayPrefix = false;
		long hour;
		if (index < span.Length && span[index] == '.')
		{
			index++;
			hasDayPrefix = true;
			if (!Int128.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out days))
				return DurationOutcome.Malformed;
			if (!TryReadDigits(span, ref index, 1, 2, out var hourDigits) || !TryRange(hourDigits, 0, 23, out hour))
				return DurationOutcome.Malformed;
		}
		else
		{
			if (first.Length > 2 || !TryRange(first, 0, 23, out hour))
				return DurationOutcome.Malformed;
		}

		if (index >= span.Length || span[index] != ':')
			return DurationOutcome.Malformed;
		index++;
		if (!TryReadDigits(span, ref index, 1, 2, out var minuteDigits) || !TryRange(minuteDigits, 0, 59, out var minute))
			return DurationOutcome.Malformed;

		long second = 0;
		Int128 fractionTicks = 0;
		if (index < span.Length && span[index] == ':')
		{
			index++;
			if (!TryReadDigits(span, ref index, 1, 2, out var secondDigits) || !TryRange(secondDigits, 0, 59, out second))
				return DurationOutcome.Malformed;

			if (index < span.Length && span[index] is '.' or ',')
			{
				index++;
				var fracStart = index;
				while (index < span.Length && char.IsAsciiDigit(span[index]))
					index++;
				var fracLen = index - fracStart;
				if (fracLen is 0 or > 9)
					return DurationOutcome.Malformed;
				fractionTicks = TruncateFractionToTicks(span[fracStart..index]);
			}
		}

		if (index != span.Length)
			return DurationOutcome.Malformed;

		var magnitude = (hasDayPrefix ? days * TimeSpan.TicksPerDay : 0) +
			((Int128)hour * TimeSpan.TicksPerHour) +
			((Int128)minute * TimeSpan.TicksPerMinute) +
			((Int128)second * TimeSpan.TicksPerSecond) +
			fractionTicks;

		return Finish(negative, magnitude, out result);
	}

	static bool TryReadDigits(ReadOnlySpan<char> span, ref int index, int minLength, int maxLength, out ReadOnlySpan<char> digits)
	{
		var start = index;
		while (index < span.Length && char.IsAsciiDigit(span[index]))
			index++;
		digits = span[start..index];
		return digits.Length >= minLength && digits.Length <= maxLength;
	}

	static bool TryRange(ReadOnlySpan<char> digits, long min, long max, out long value) =>
		long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;

	// Ticks are 100ns (seven decimal digits of a second); a declared fraction beyond that
	// truncates -- never rounds. Digits beyond the first seven are simply not read.
	static Int128 TruncateFractionToTicks(ReadOnlySpan<char> digits)
	{
		Span<char> padded = stackalloc char[7];
		for (var i = 0; i < padded.Length; i++)
			padded[i] = i < digits.Length ? digits[i] : '0';
		return long.Parse(padded, CultureInfo.InvariantCulture);
	}

	// The single choke point every grammar (ISO/colon/seconds) funnels through once it has an
	// unsigned tick magnitude in hand: past MaxTicksMagnitude is OutOfRange (this is also where
	// TimeSpan.MinValue/MaxValue themselves land -- both sit far past the ±10,000-year ceiling,
	// so no separate sentinel check is needed); otherwise the sign is applied and the value fits
	// a long by construction.
	static DurationOutcome Finish(bool negative, Int128 magnitudeTicks, out TimeSpan result)
	{
		result = TimeSpan.Zero;
		if (magnitudeTicks > _maxTicksMagnitude)
			return DurationOutcome.OutOfRange;
		var ticks = (long)magnitudeTicks;
		result = new TimeSpan(negative ? -ticks : ticks);
		return DurationOutcome.Ok;
	}

	enum DurationOutcome
	{
		Malformed,
		OutOfRange,
		Ok
	}
}
