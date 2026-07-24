namespace Norse.Primitives.Tests;

public sealed class TemporalFusionTests
{
	const string AllWhitespace = " \t\r\n\f ";

	// ── Happy path ──────────────────────────────────────────────────────────

	[Fact]
	void Should_fuse_to_utc_datetime_when_all_inputs_are_valid_standard_time()
	{
		// 2026-01-02 15:04:05 CST (UTC-6) = 2026-01-02 21:04:05 UTC
		var actual = TemporalFusion.FuseRequired("2026-01-02", "15:04:05", "America/Chicago");
		actual.TryGetValue(out Success<DateTime> success).ShouldBeTrue();
		success.Value.Kind.ShouldBe(DateTimeKind.Utc);
		success.Value.ShouldBe(new(2026, 1, 2, 21, 4, 5, DateTimeKind.Utc));
	}

	[Fact]
	void Should_fuse_to_utc_datetime_when_all_inputs_are_valid_daylight_time()
	{
		// 2026-06-15 10:00:00 CDT (UTC-5) = 2026-06-15 15:00:00 UTC
		// Proves the conversion is date-aware (not a fixed +6 hour offset).
		var actual = TemporalFusion.FuseRequired("2026-06-15", "10:00:00", "America/Chicago");
		actual.TryGetValue(out Success<DateTime> success).ShouldBeTrue();
		success.Value.Kind.ShouldBe(DateTimeKind.Utc);
		success.Value.ShouldBe(new(2026, 6, 15, 15, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	void Should_fuse_when_optional_and_both_fields_are_present()
	{
		var actual = TemporalFusion.FuseOptional("2026-01-02", "12:00:00", "UTC");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<DateTime> success).ShouldBeTrue();
		success.Value.Kind.ShouldBe(DateTimeKind.Utc);
	}

	// ── DST seam failures ───────────────────────────────────────────────────

	[Fact]
	void Should_fail_with_dst_gap_detail_when_wall_clock_falls_in_spring_forward()
	{
		// 2026-03-08: clocks spring forward from 2:00 to 3:00 AM in America/Chicago.
		// 02:30 never existed — the BCL would throw on ConvertTimeToUtc, so we check first.
		var actual = TemporalFusion.FuseRequired("2026-03-08", "02:30:00", "America/Chicago");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateTime");
		failure.Detail.ShouldBe("DST gap");
		failure.Input.ShouldBe("2026-03-08T02:30 America/Chicago");
	}

	[Fact]
	void Should_fail_with_dst_ambiguous_detail_when_wall_clock_falls_in_fall_back()
	{
		// 2026-11-01: clocks fall back from 2:00 to 1:00 AM in America/Chicago.
		// 01:30 occurs twice — the BCL silently picks standard time; we refuse to guess.
		var actual = TemporalFusion.FuseRequired("2026-11-01", "01:30:00", "America/Chicago");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateTime");
		failure.Detail.ShouldBe("DST ambiguous");
		failure.Input.ShouldBe("2026-11-01T01:30 America/Chicago");
	}

	// ── Sub-failure propagation (first-failure-wins: date → time → zone) ───

	[Fact]
	void Should_propagate_date_failure_verbatim_when_date_is_malformed()
	{
		// All three inputs bad — date is checked first.
		var actual = TemporalFusion.FuseRequired("garbage", "also-bad", "Not/A/Zone");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateOnly");
		failure.Format.ShouldBe("ISO 8601");
	}

	[Fact]
	void Should_propagate_time_failure_verbatim_when_date_is_good_but_time_is_malformed()
	{
		var actual = TemporalFusion.FuseRequired("2026-01-02", "also-bad", "Not/A/Zone");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("TimeOnly");
		failure.Format.ShouldBe("ISO 8601");
	}

	[Fact]
	void Should_propagate_zone_failure_verbatim_when_date_and_time_are_good_but_zone_is_unrecognized()
	{
		var actual = TemporalFusion.FuseRequired("2026-01-02", "15:04:05", "Not/A/Zone");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("TimeZoneInfo");
		failure.Format.ShouldBe("IANA");
	}

	// ── Partial input ────────────────────────────────────────────────────────

	[Fact]
	void Should_fail_with_partial_instant_detail_when_date_is_present_but_time_is_absent()
	{
		var actual = TemporalFusion.FuseRequired("2026-01-02", "", "America/Chicago");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateTime");
		failure.Detail.ShouldBe("partial instant");
		failure.Input.ShouldBe("2026-01-02");
	}

	[Fact]
	void Should_fail_with_partial_instant_detail_when_time_is_present_but_date_is_absent()
	{
		var actual = TemporalFusion.FuseRequired("", "15:04:05", "America/Chicago");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateTime");
		failure.Detail.ShouldBe("partial instant");
		failure.Input.ShouldBe("15:04:05");
	}

	[Fact]
	void Should_return_partial_failure_on_optional_door_when_exactly_one_field_is_absent()
	{
		// Optional door: partial is still an error, not absence.
		var actual = TemporalFusion.FuseOptional("2026-01-02", "", "America/Chicago");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Detail.ShouldBe("partial instant");
		failure.Input.ShouldBe("2026-01-02");
	}

	// ── Absence (both fields empty — zone is not consulted) ─────────────────

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_fail_with_empty_reason_when_required_date_and_time_are_both_absent(string? dateAndTime)
	{
		var actual = TemporalFusion.FuseRequired(dateAndTime, dateAndTime, "America/Chicago");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.ExpectedType.ShouldBe("DateTime");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(AllWhitespace)]
	void Should_return_absent_when_optional_date_and_time_are_both_absent(string? dateAndTime) =>
		TemporalFusion.FuseOptional(dateAndTime, dateAndTime, "America/Chicago").HasValue.ShouldBeFalse();

	[Fact]
	void Should_not_consult_zone_when_both_date_and_time_are_absent()
	{
		// An invalid zone with both fields empty must still return Empty/null — not a zone failure.
		TemporalFusion.FuseRequired("", "", "Not/A/Zone")
			.TryGetValue(out Failure required).ShouldBeTrue();
		required.Reason.ShouldBe(ParseFailure.Empty);
		TemporalFusion.FuseOptional("", "", "Not/A/Zone").HasValue.ShouldBeFalse();
	}

	// ── Sentinel guard ───────────────────────────────────────────────────────

	[Fact]
	void Should_propagate_date_sentinel_failure_when_date_is_datetime_minvalue()
	{
		// DateOnlyParser blocks DateOnly.MinValue (0001-01-01) before TemporalFusion reaches
		// its own UTC sentinel guard. The sub-parser is the first line of defense.
		var actual = TemporalFusion.FuseRequired("0001-01-01", "00:00:00", "UTC");
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("DateOnly");
	}
}
