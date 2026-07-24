using System.Globalization;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="char"/>. A single-character input is that character verbatim
/// — whitespace included, never trimmed away — and any longer input is read as a code point in
/// decimal (<c>65</c>), hex/<c>U+</c> (<c>0x41</c>, <c>&amp;H41</c>, <c>U+0041</c>), or HTML-entity
/// form (<c>&amp;#65;</c>, <c>&amp;#x41;</c>). Culture-insensitive — no format provider.
/// </summary>
/// <remarks>
/// The single-character rule has precedence by design: <c>"5"</c> is the literal <c>'5'</c>, never
/// code point 5. Code points are validated to the UTF-16 range 0..65535.
/// </remarks>
public static class CharParser
{
	const string ExpectedType = nameof(Char);

	/// <summary>
	/// Parses required character text. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; unrecognized input is
	/// <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <param name="input">The raw scalar text. A single character is taken verbatim.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<char> ParseRequired(ReadOnlySpan<char> input)
	{
		if (input.Length == 1)
			return new Success<char>(input[0]);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			Parse(trimmed);
	}

	/// <summary>
	/// Parses optional character text. Empty or whitespace input is absent
	/// (<see langword="null"/>); unrecognized input is <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <param name="input">The raw scalar text. A single character is taken verbatim.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<char>? ParseOptional(ReadOnlySpan<char> input)
	{
		if (input.Length == 1)
			return new Success<char>(input[0]);
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			null :
			Parse(trimmed);
	}

	static Result<char> Parse(ReadOnlySpan<char> trimmed) =>
		trimmed.Length == 1 ?
			new Success<char>(trimmed[0]) :
			TryCodePoint(trimmed, out var point) ?
				new Success<char>(point) :
				TryHtmlEntity(trimmed, out var entity) ?
					new Success<char>(entity) :
					new Failure(ParseFailure.Malformed, trimmed, ExpectedType);

	static bool TryCodePoint(ReadOnlySpan<char> trimmed, out char value)
	{
		int code;
		if (trimmed.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ||
			trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
			trimmed.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
		{
			if (int.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
				return InRange(code, out value);
			value = '\0';
			return false;
		}

		if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out code))
			return InRange(code, out value);
		value = '\0';
		return false;
	}

	static bool TryHtmlEntity(ReadOnlySpan<char> trimmed, out char value)
	{
		if (trimmed.Length < 4 || trimmed[0] != '&' || trimmed[1] != '#' || trimmed[^1] != ';')
		{
			value = '\0';
			return false;
		}
		var inner = trimmed[2..^1];
		int code;
		if (inner.StartsWith("x", StringComparison.OrdinalIgnoreCase))
		{
			if (int.TryParse(inner[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
				return InRange(code, out value);
			value = '\0';
			return false;
		}

		if (int.TryParse(inner, NumberStyles.None, CultureInfo.InvariantCulture, out code))
			return InRange(code, out value);
		value = '\0';
		return false;
	}

	static bool InRange(int code, out char value)
	{
		if (code is >= 0 and <= char.MaxValue)
		{
			value = (char)code;
			return true;
		}
		value = '\0';
		return false;
	}
}
