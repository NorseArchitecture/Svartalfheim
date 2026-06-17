using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="TimeOnly"/>. The ISO door accepts the 24-hour profile
/// <c>HH:mm:ss[.fffffff]</c> and <c>HH:mm</c> under <see cref="CultureInfo.InvariantCulture"/>; the
/// exact door accepts a single caller-declared format (e.g. 12-hour <c>h:mm:ss tt</c>) under a
/// required provider. No sentinel guard — <see cref="TimeOnly.MinValue"/> (midnight) and
/// <see cref="TimeOnly.MaxValue"/> are real clock readings. Culture-insensitive on the ISO door.
/// </summary>
public static class TimeOnlyParser
{
	const string ExpectedType = nameof(TimeOnly);
	const string IsoLabel = "ISO 8601";

	static readonly string[] _isoFormats = ["HH:mm:ss.FFFFFFF", "HH:mm:ss", "HH:mm"];

	/// <summary>Parses an ISO 24-hour time. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<TimeOnly> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return ParseIso(trimmed);
	}

	/// <summary>Parses an optional ISO time. Empty ⇒ absent; unrecognized ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<TimeOnly>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return ParseIso(trimmed);
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
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return ParseExact(trimmed, format, provider);
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
		if (trimmed.IsEmpty)
			return null;
		return ParseExact(trimmed, format, provider);
	}

	static Result<TimeOnly> ParseIso(ReadOnlySpan<char> trimmed)
	{
		if (TimeOnly.TryParseExact(trimmed, _isoFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
			return new Success<TimeOnly>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);
	}

	static Result<TimeOnly> ParseExact(ReadOnlySpan<char> trimmed, string format, IFormatProvider provider)
	{
		if (TimeOnly.TryParseExact(trimmed, format, provider, DateTimeStyles.AllowWhiteSpaces, out var value))
			return new Success<TimeOnly>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, format);
	}
}
