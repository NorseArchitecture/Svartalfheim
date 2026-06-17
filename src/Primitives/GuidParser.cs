namespace Norse.Primitives;

/// <summary>
/// Span-based parser for <see cref="Guid"/>. Strips a leading case-insensitive
/// <c>urn:uuid:</c> / <c>GUID:</c> / <c>UUID:</c> prefix, then parses every format
/// <see cref="Guid.TryParse(ReadOnlySpan{char}, out Guid)"/> accepts (N, D, B, P, X).
/// Culture-insensitive — no format provider.
/// </summary>
public static class GuidParser
{
	const string ExpectedType = nameof(Guid);

	static readonly string[] _prefixes = ["urn:uuid:", "GUID:", "UUID:"];

	/// <summary>
	/// Parses required GUID text. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; unrecognized input is
	/// <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns>The parse outcome — never throws on bad input.</returns>
	public static Result<Guid> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		return Parse(trimmed);
	}

	/// <summary>
	/// Parses optional GUID text. Empty or whitespace input is absent
	/// (<see langword="null"/>); unrecognized input is <see cref="ParseFailure.Malformed"/>.
	/// </summary>
	/// <param name="input">The raw scalar text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the parse outcome.</returns>
	public static Result<Guid>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		if (trimmed.IsEmpty)
			return null;
		return Parse(trimmed);
	}

	static Result<Guid> Parse(ReadOnlySpan<char> trimmed)
	{
		if (Guid.TryParse(StripPrefix(trimmed), out var value))
			return new Success<Guid>(value);
		return new Failure(ParseFailure.Malformed, trimmed, ExpectedType);
	}

	static ReadOnlySpan<char> StripPrefix(ReadOnlySpan<char> trimmed)
	{
		foreach (var prefix in _prefixes)
			if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				return trimmed[prefix.Length..].Trim();
		return trimmed;
	}
}
