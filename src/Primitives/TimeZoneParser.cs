namespace Norse.Primitives;

/// <summary>
/// Span-based resolver for <see cref="TimeZoneInfo"/>. Resolves untrusted IANA zone-id text
/// against the OS/ICU zone database via
/// <see cref="TimeZoneInfo.TryFindSystemTimeZoneById(string, out TimeZoneInfo?)"/>. Empty or
/// whitespace input is <see cref="ParseFailure.Empty"/> or absent; an unrecognized id is
/// <see cref="ParseFailure.Malformed"/> with <see cref="Failure.Format"/> = <c>"IANA"</c>.
/// Culture-insensitive — no <see cref="IFormatProvider"/>. Off-gateway by construction:
/// <see cref="TimeZoneInfo"/> does not implement <see cref="ISpanParsable{TSelf}"/>.
/// </summary>
/// <remarks>
/// Resolving untrusted boundary text against a known table is parsing — the same way
/// culture-sensitive numeric parsing consults ICU. A zone-id lookup hitting the OS/ICU zone
/// database belongs on the forge. No silent fallback to <see cref="TimeZoneInfo.Local"/> or
/// <see cref="TimeZoneInfo.Utc"/> — a missing or unrecognized zone is a loud failure.
/// </remarks>
public static class TimeZoneParser
{
	const string
		ExpectedType = nameof(TimeZoneInfo),
		IanaLabel = "IANA";

	/// <summary>
	/// Resolves a required IANA zone id. Empty or whitespace input is a
	/// <see cref="ParseFailure.Empty"/> failure; an unrecognized id is
	/// <see cref="ParseFailure.Malformed"/> (<see cref="Failure.Format"/> = <c>"IANA"</c>).
	/// </summary>
	/// <param name="input">The raw zone-id text. A null string converts to the empty span.</param>
	/// <returns>The resolve outcome — never throws on bad input.</returns>
	public static Result<TimeZoneInfo> ParseRequired(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			new Failure(ParseFailure.Empty, string.Empty, ExpectedType) :
			Resolve(trimmed);
	}

	/// <summary>
	/// Resolves an optional IANA zone id. Empty or whitespace input is absent
	/// (<see langword="null"/>); an unrecognized id is
	/// <see cref="ParseFailure.Malformed"/> (<see cref="Failure.Format"/> = <c>"IANA"</c>).
	/// </summary>
	/// <param name="input">The raw zone-id text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when absent; otherwise the resolve outcome.</returns>
	public static Result<TimeZoneInfo>? ParseOptional(ReadOnlySpan<char> input)
	{
		var trimmed = input.Trim();
		return trimmed.IsEmpty ?
			(Result<TimeZoneInfo>?)null :
			Resolve(trimmed);
	}

	static Result<TimeZoneInfo> Resolve(ReadOnlySpan<char> trimmed) =>
		TimeZoneInfo.TryFindSystemTimeZoneById(trimmed.ToString(), out var zone) ?
			new Success<TimeZoneInfo>(zone) :
			new Failure(ParseFailure.Malformed, trimmed, ExpectedType, IanaLabel);
}
