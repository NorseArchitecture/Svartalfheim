using System.Collections.Frozen;

namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="bool"/>. Extends
/// <see cref="bool.TryParse(ReadOnlySpan{char}, out bool)"/> with the numeric and
/// natural-language conventions untrusted sources actually send.
/// </summary>
/// <remarks>
/// Recognized true values: <c>true</c>, <c>t</c>, <c>yes</c>, <c>y</c>, <c>1</c>,
/// <c>on</c>, <c>enabled</c>, <c>active</c>, <c>checked</c>, <c>in</c>.
/// Recognized false values: <c>false</c>, <c>f</c>, <c>no</c>, <c>n</c>, <c>0</c>,
/// <c>off</c>, <c>disabled</c>, <c>inactive</c>, <c>unchecked</c>, <c>out</c>.
/// Matching is case-insensitive; leading and trailing whitespace is ignored.
/// Boolean text is culture-insensitive, so no format provider is accepted.
/// </remarks>
public static class BooleanParser
{
	const string ExpectedType = nameof(Boolean);

	static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _trueValues =
		new[] { "t", "yes", "y", "1", "on", "enabled", "active", "checked", "in" }
			.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
			.GetAlternateLookup<ReadOnlySpan<char>>();

	static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _falseValues =
		new[] { "f", "no", "n", "0", "off", "disabled", "inactive", "unchecked", "out" }
			.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
			.GetAlternateLookup<ReadOnlySpan<char>>();

	/// <summary>
	/// Parses required boolean text. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; unrecognized input is
	/// <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<bool> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return Parse(trimmed);
	}

	/// <summary>
	/// Parses optional boolean text. Empty or whitespace input is absent
	/// (<see langword="null"/>); unrecognized input is <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<bool>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return Parse(trimmed);
	}

	static Result<bool> Parse(ReadOnlySpan<char> trimmed)
	{
		if (bool.TryParse(trimmed, out var parsed))
			return new Success<bool>(parsed);
		if (_trueValues.Contains(trimmed))
			return new Success<bool>(true);
		if (_falseValues.Contains(trimmed))
			return new Success<bool>(false);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType);
	}
}
