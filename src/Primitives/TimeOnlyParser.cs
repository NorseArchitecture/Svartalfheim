using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="TimeOnly"/>. The ISO door accepts the 24-hour profile
/// <c>HH:mm[:ss[.f{1..9}]]</c> under <see cref="CultureInfo.InvariantCulture"/> — one to nine
/// fractional-second digits, the eighth and ninth truncating (never rounding) to tick precision,
/// matching HyperCast's own grammar; a bare trailing <c>.</c> with no digits is
/// <see cref="ParseFailure.Malformed"/>, not a silent zero fraction. The exact door accepts a
/// single caller-declared format (e.g. 12-hour <c>h:mm:ss tt</c>) under a required provider. No
/// sentinel guard — <see cref="TimeOnly.MinValue"/> (midnight) and <see cref="TimeOnly.MaxValue"/>
/// are real clock readings; this door has no range failure either (a well-formed
/// <c>HH:mm[:ss[.f]]</c> token is always representable). Culture-insensitive on the ISO door.
/// </summary>
public static class TimeOnlyParser
{
	const string
		ExpectedType = nameof(TimeOnly),
		IsoLabel = "ISO 8601",
		ShortFormat = "HH:mm",
		LocalFormat = "HH:mm:ss";

	// "HH:mm" / "HH:mm:ss" -- fixed widths so the fractional-second scan below always starts at a
	// compile-time-constant offset, same technique as DateTimeOffsetParser's LocalLength.
	const int
		ShortLength = 5,
		LocalLength = 8;

	/// <summary>Parses an ISO 24-hour time. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<TimeOnly> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseIso(trimmed);
	}

	/// <summary>Parses an optional ISO time. Empty ⇒ absent; unrecognized ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<TimeOnly>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseIso(trimmed);
	}

	/// <summary>Parses a time against a single caller-declared <paramref name="format"/>.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<TimeOnly> ParseExactRequired(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseExact(trimmed, format, provider);
	}

	/// <summary>Parses an optional time against a single caller-declared <paramref name="format"/>.</summary>
	/// <param name="input">The raw scalar text.</param>
	/// <param name="format">The exact format. Required, non-empty.</param>
	/// <param name="provider">The declared culture. Never null.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	/// <exception cref="ArgumentException"><paramref name="format"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
	public static Result<TimeOnly>? ParseExactOptional(ReadOnlySpan<char> input, string format, IFormatProvider provider)
	{
		ArgumentException.ThrowIfNullOrEmpty(format);
		ArgumentNullException.ThrowIfNull(provider);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			ParseExact(trimmed, format, provider);
	}

	static Result<TimeOnly> ParseIso(ReadOnlySpan<char> trimmed)
	{
		if (NativeCapability.Available)
			return HyperCast.Cast.Time(trimmed) switch
			{
				HyperCast.Success<TimeOnly> s => new Success<TimeOnly>(s.Value),
				HyperCast.Fault { Reason: HyperCast.CastFailure.OutOfRange } => new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType, IsoLabel),
				HyperCast.Fault => new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel),
			};

		return ParseIsoManaged(trimmed);
	}

	// Hand-rolled instead of a fixed TryParseExact format array: the BCL's own "F"-specifier
	// leniency accepts a trailing "." with zero fraction digits (a silent zero) and has no way to
	// express "one to nine digits, truncated past seven" -- both genuine corpus divergences (a
	// standing directive-1 convergence, not a BCL leniency this realm chooses to keep).
	static Result<TimeOnly> ParseIsoManaged(ReadOnlySpan<char> trimmed)
	{
		if (trimmed.Length == ShortLength)
			return TimeOnly.TryParseExact(trimmed, ShortFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var shortValue) ?
				new Success<TimeOnly>(shortValue) :
				new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);

		if (trimmed.Length < LocalLength || trimmed[2] != ':' || trimmed[5] != ':' ||
			!TimeOnly.TryParseExact(trimmed[..LocalLength], LocalFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);

		var rest = trimmed[LocalLength..];
		if (rest.IsEmpty)
			return new Success<TimeOnly>(value);

		// One to nine fractional digits only -- a bare "." with nothing after it, or a tenth digit
		// onward (no .NET tick-level representation), is Malformed rather than a silent truncation
		// or a silent zero.
		if (rest[0] != '.')
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);
		var digits = rest[1..];
		if (digits.Length is 0 or > 9 || !AllAsciiDigits(digits))
			return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);

		return new Success<TimeOnly>(value.Add(TimeSpan.FromTicks(TruncateToTicks(digits))));
	}

	static bool AllAsciiDigits(ReadOnlySpan<char> digits)
	{
		foreach (var c in digits)
			if (!char.IsAsciiDigit(c))
				return false;
		return true;
	}

	// Ticks are 100ns (seven decimal digits of a second); an eighth or ninth fractional digit
	// truncates -- never rounds -- matching HyperCast's own documented sub-tick behavior.
	static int TruncateToTicks(ReadOnlySpan<char> digits)
	{
		Span<char> padded = stackalloc char[7];
		for (var i = 0; i < padded.Length; i++)
			padded[i] = i < digits.Length ? digits[i] : '0';
		return int.Parse(padded, CultureInfo.InvariantCulture);
	}

	static Result<TimeOnly> ParseExact(ReadOnlySpan<char> trimmed, string format, IFormatProvider provider) =>
		TimeOnly.TryParseExact(trimmed, format, provider, DateTimeStyles.AllowWhiteSpaces, out var value) ?
			new Success<TimeOnly>(value) :
			new Failure(ParseFailure.Malformed, trimmed, ExpectedType, format);
}
