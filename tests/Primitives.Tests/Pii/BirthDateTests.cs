using Norse.Primitives.Pii;

namespace Norse.Primitives.Tests.Pii;

public sealed class BirthDateTests
{
	[Fact]
	void Should_parse_strict_iso_when_input_is_valid()
	{
		BirthDate.Parse("1988-04-12").TryGetValue(out Success<BirthDate> success).ShouldBeTrue();
		success.Value.Value.ShouldBe(new DateOnly(1988, 4, 12));
		success.Value.WireValue.ShouldBe("1988-04-12");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	void Should_fail_with_empty_reason_when_input_is_blank(string input)
	{
		BirthDate.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
	}

	[Theory]
	[InlineData("04/12/1988")]
	[InlineData("1988-4-12")]
	[InlineData("1988-13-01")]
	[InlineData("19880412")]
	[InlineData("not-a-date")]
	void Should_fail_with_malformed_reason_when_format_is_not_strict_iso(string input)
	{
		BirthDate.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_redact_completely_when_pure_mask_is_rendered()
	{
		BirthDate.Parse("1988-04-12").TryGetValue(out Success<BirthDate> success).ShouldBeTrue();
		success.Value.Masked.ShouldBe("****-**-**");
		success.Value.ToString().ShouldBe("****-**-**");
	}

	[Theory]
	[InlineData("1988-04-12", 2026, 8, 3, "38")]   // birthday passed this year
	[InlineData("1988-09-12", 2026, 8, 3, "37")]   // birthday not yet reached
	[InlineData("1988-08-03", 2026, 8, 3, "38")]   // birthday is today
	[InlineData("2027-01-01", 2026, 8, 3, "0")]    // future date clamps to zero
	void Should_compute_exact_age_when_disclosure_mask_is_requested(string birth, int y, int m, int d, string expected)
	{
		BirthDate.Parse(birth).TryGetValue(out Success<BirthDate> success).ShouldBeTrue();
		success.Value.ToMasked(new DateOnly(y, m, d)).ShouldBe(expected);
	}

	[Fact]
	void Should_test_the_leap_day_boundary_when_computing_age()
	{
		// Born Feb 29; on Feb 28 of a non-leap year the birthday has not occurred yet.
		BirthDate.Parse("2000-02-29").TryGetValue(out Success<BirthDate> success).ShouldBeTrue();
		success.Value.ToMasked(new DateOnly(2026, 2, 28)).ShouldBe("25");
		success.Value.ToMasked(new DateOnly(2026, 3, 1)).ShouldBe("26");
	}

	[Fact]
	void Should_return_the_canonical_iso_string_when_normalized_is_requested()
	{
		BirthDate.Parse("1988-04-12").TryGetValue(out Success<BirthDate> success).ShouldBeTrue();
		success.Value.Normalized.ShouldBe("1988-04-12");
		success.Value.Normalized.ShouldBe(success.Value.WireValue);
	}

	[Fact]
	void Should_throw_when_default_instance_is_accessed()
	{
		BirthDate malformed = default;
		Should.Throw<InvalidOperationException>(() => malformed.WireValue);
	}
}
