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
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			Parse(trimmed);
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
		return trimmed.IsEmpty ?
			null :
			Parse(trimmed);
	}

	static Result<Guid> Parse(ReadOnlySpan<char> trimmed)
	{
		if (NativeCapability.Available)
			return HyperCast.Cast.Uuid(StripPrefix(trimmed)) switch
			{
				HyperCast.Success<Guid> s => new Success<Guid>(s.Value),
				HyperCast.Fault { Reason: HyperCast.CastFailure.OutOfRange } => new Failure(ParseFailure.OutOfRange, trimmed, ExpectedType),
				HyperCast.Fault => new Failure(ParseFailure.Malformed, trimmed, ExpectedType),
			};

		return Guid.TryParse(StripPrefix(trimmed), out var value) ?
			new Success<Guid>(value) :
			new Failure(ParseFailure.Malformed, trimmed, ExpectedType);
	}

	static ReadOnlySpan<char> StripPrefix(ReadOnlySpan<char> trimmed)
	{
		foreach (var prefix in _prefixes)
			if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				return trimmed[prefix.Length..].Trim();
		return trimmed;
	}
}
