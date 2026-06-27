namespace Norse.Primitives;

/// <summary>
/// Composition specialist: three <see cref="ReadOnlySpan{T}"/> text inputs (ISO 8601 date, ISO
/// 8601 24-hour time, IANA zone id) → one UTC <see cref="DateTime"/> (<see cref="DateTimeKind.Utc"/>).
/// </summary>
/// <remarks>
/// <para>
/// Each input is parsed independently through the established ISO-canonical doors —
/// <see cref="DateOnlyParser.ParseRequired"/>, <see cref="TimeOnlyParser.ParseRequired"/>,
/// <see cref="TimeZoneParser.ParseRequired"/> — in the documented date → time → zone order;
/// the first sub-parse that fails returns its <see cref="Failure"/> verbatim. Both DST seams
/// are checked before the BCL conversion: a spring-forward gap is
/// <see cref="ParseFailure.Malformed"/> with <see cref="Failure.Detail"/> = <c>"DST gap"</c>;
/// a fall-back ambiguity is <see cref="ParseFailure.Malformed"/> with
/// <see cref="Failure.Detail"/> = <c>"DST ambiguous"</c>. The BCL's silent standard-time pick
/// for an ambiguous wall-clock never occurs.
/// </para>
/// <para>
/// The sentinel guard (§4 of the temporal-parsers spec) applies to the fused result:
/// <see cref="DateTime.MinValue"/>/<see cref="DateTime.MaxValue"/> are
/// <see cref="ParseFailure.Malformed"/>. Culture-insensitive — no
/// <see cref="IFormatProvider"/>. Off-gateway (no new branch in <see cref="Parser"/>).
/// </para>
/// <para>
/// Optionality is evaluated on the date and time fields only — the zone is infrastructure,
/// never the thing that makes a value absent. Both fields empty ⇒ absent; exactly one empty ⇒
/// <see cref="ParseFailure.Malformed"/> (<see cref="Failure.Detail"/> = <c>"partial instant"</c>).
/// </para>
/// </remarks>
public static class TemporalFusion
{
	const string ExpectedType = nameof(DateTime);

	/// <summary>
	/// Fuses an ISO date, an ISO time, and an IANA zone id into a UTC <see cref="DateTime"/>.
	/// Both fields empty ⇒ <see cref="ParseFailure.Empty"/>; exactly one empty ⇒
	/// <see cref="ParseFailure.Malformed"/> <c>Detail = "partial instant"</c>; sub-parse failures
	/// propagate verbatim in the order date → time → zone; DST gap ⇒ <c>Detail = "DST gap"</c>;
	/// DST ambiguity ⇒ <c>Detail = "DST ambiguous"</c>.
	/// </summary>
	/// <param name="date">The ISO 8601 <c>yyyy-MM-dd</c> text. A null string converts to the empty span.</param>
	/// <param name="time">The ISO 8601 24-hour time text. A null string converts to the empty span.</param>
	/// <param name="zone">The IANA zone id text. A null string converts to the empty span.</param>
	/// <returns>The fuse outcome — never throws on bad input.</returns>
	public static Result<DateTime> FuseRequired(ReadOnlySpan<char> date, ReadOnlySpan<char> time, ReadOnlySpan<char> zone)
	{
		var dateTrimmed = date.Trim();
		var timeTrimmed = time.Trim();
		if (dateTrimmed.IsEmpty && timeTrimmed.IsEmpty)
			return new Failure(ParseFailure.Empty, string.Empty, ExpectedType);
		if (dateTrimmed.IsEmpty || timeTrimmed.IsEmpty)
			return new Failure(ParseFailure.Malformed, dateTrimmed.IsEmpty ? timeTrimmed : dateTrimmed, ExpectedType, null, "partial instant");
		return Fuse(date, time, zone);
	}

	/// <summary>
	/// Fuses an ISO date, an ISO time, and an IANA zone id into an optional UTC <see cref="DateTime"/>.
	/// Both fields empty ⇒ absent (<see langword="null"/>); exactly one empty ⇒
	/// <see cref="ParseFailure.Malformed"/> <c>Detail = "partial instant"</c>; sub-parse failures
	/// propagate verbatim in the order date → time → zone; DST gap ⇒ <c>Detail = "DST gap"</c>;
	/// DST ambiguity ⇒ <c>Detail = "DST ambiguous"</c>.
	/// </summary>
	/// <param name="date">The ISO 8601 <c>yyyy-MM-dd</c> text. A null string converts to the empty span.</param>
	/// <param name="time">The ISO 8601 24-hour time text. A null string converts to the empty span.</param>
	/// <param name="zone">The IANA zone id text. A null string converts to the empty span.</param>
	/// <returns><see langword="null"/> when both date and time are absent; otherwise the fuse outcome.</returns>
	public static Result<DateTime>? FuseOptional(ReadOnlySpan<char> date, ReadOnlySpan<char> time, ReadOnlySpan<char> zone)
	{
		var dateTrimmed = date.Trim();
		var timeTrimmed = time.Trim();
		if (dateTrimmed.IsEmpty && timeTrimmed.IsEmpty)
			return null;
		if (dateTrimmed.IsEmpty || timeTrimmed.IsEmpty)
			return new Failure(ParseFailure.Malformed, dateTrimmed.IsEmpty ? timeTrimmed : dateTrimmed, ExpectedType, null, "partial instant");
		return Fuse(date, time, zone);
	}

	static Result<DateTime> Fuse(ReadOnlySpan<char> date, ReadOnlySpan<char> time, ReadOnlySpan<char> zone)
	{
		var dateResult = DateOnlyParser.ParseRequired(date);
		if (!dateResult.TryGetValue(out Success<DateOnly> dateSuccess))
		{
			dateResult.TryGetValue(out Failure dateFailure);
			return dateFailure;
		}
		var timeResult = TimeOnlyParser.ParseRequired(time);
		if (!timeResult.TryGetValue(out Success<TimeOnly> timeSuccess))
		{
			timeResult.TryGetValue(out Failure timeFailure);
			return timeFailure;
		}
		var zoneResult = TimeZoneParser.ParseRequired(zone);
		if (!zoneResult.TryGetValue(out Success<TimeZoneInfo> zoneSuccess))
		{
			zoneResult.TryGetValue(out Failure zoneFailure);
			return zoneFailure;
		}
		return ConvertToUtc(dateSuccess.Value, timeSuccess.Value, zoneSuccess.Value);
	}

	static Result<DateTime> ConvertToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
	{
		var wall = date.ToDateTime(time, DateTimeKind.Unspecified);
		var compositeInput = $"{wall:yyyy-MM-ddTHH:mm} {zone.Id}";
		if (zone.IsInvalidTime(wall))
			return new Failure(ParseFailure.Malformed, compositeInput, ExpectedType, null, "DST gap");
		if (zone.IsAmbiguousTime(wall))
			return new Failure(ParseFailure.Malformed, compositeInput, ExpectedType, null, "DST ambiguous");
		var utc = TimeZoneInfo.ConvertTimeToUtc(wall, zone);
		if (utc == DateTime.MinValue || utc == DateTime.MaxValue)
			return new Failure(ParseFailure.Malformed, compositeInput, ExpectedType);
		return new Success<DateTime>(utc);
	}
}
