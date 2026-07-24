using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="DateOnly"/>. The ISO door accepts exactly
/// <c>yyyy-MM-dd</c> under <see cref="CultureInfo.InvariantCulture"/>; the exact door accepts a
/// single caller-declared format under a required provider. The sentinel guard rejects
/// <see cref="DateOnly.MinValue"/> and <see cref="DateOnly.MaxValue"/> — neither ever reflects valid
/// state. Culture-insensitive on the ISO door (no provider — ISO 8601 is invariant).
/// </summary>
public static class DateOnlyParser
{
	const string
		ExpectedType = nameof(DateOnly),
		IsoFormat = "yyyy-MM-dd",
		IsoLabel = "ISO 8601";

	/// <summary>Parses an ISO <c>yyyy-MM-dd</c> date. Empty ⇒ <see cref="ParseFailure.Empty"/>; unrecognized or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<DateOnly> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			ParseIso(trimmed);
	}

	/// <summary>Parses an optional ISO date. Empty ⇒ absent (<see langword="null"/>); unrecognized or sentinel ⇒ <see cref="ParseFailure.Malformed"/>.</summary>
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
		if (DateOnly.TryParseExact(trimmed, IsoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) &&
			!IsSentinel(value))
			return new Success<DateOnly>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IsoLabel);
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
