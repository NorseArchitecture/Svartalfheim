using Norse.Primitives.Pii;

namespace Norse.Primitives.Tests.Pii;

public sealed class EmailAddressTests
{
	[Fact]
	void Should_parse_and_expose_wire_and_normalized_forms_when_input_is_valid()
	{
		var result = EmailAddress.Parse("  Buvy@Example.COM ");
		result.TryGetValue(out Success<EmailAddress> success).ShouldBeTrue();
		success.Value.WireValue.ShouldBe("Buvy@Example.COM");
		success.Value.Normalized.ShouldBe("buvy@example.com");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	void Should_fail_with_empty_reason_when_input_is_blank(string input)
	{
		EmailAddress.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
	}

	[Theory]
	[InlineData("no-at-sign")]
	[InlineData("two@@ats.com")]
	[InlineData("a@b@c.com")]
	[InlineData("@nodomain.com")]
	[InlineData("nolocal@")]
	[InlineData("local@nodot")]
	[InlineData("local@.leadingdot.com")]
	[InlineData("local@trailingdot.com.")]
	[InlineData("spa ce@domain.com")]
	void Should_fail_with_malformed_reason_when_shape_is_invalid(string input)
	{
		EmailAddress.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_fail_with_malformed_reason_when_input_exceeds_max_length()
	{
		var input = $"{new string('a', 250)}@x.com";
		EmailAddress.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_mask_to_first_characters_and_tld_when_rendered()
	{
		var result = EmailAddress.Parse("jane@domain.com");
		result.TryGetValue(out Success<EmailAddress> success).ShouldBeTrue();
		success.Value.Masked.ShouldBe("j***@d***.com");
		success.Value.ToMasked(new DateOnly(2026, 8, 3)).ShouldBe("j***@d***.com");
		success.Value.ToString().ShouldBe("j***@d***.com");
	}

	[Fact]
	void Should_keep_only_the_final_label_when_domain_is_multi_label()
	{
		var result = EmailAddress.Parse("jane@mail.domain.co.uk");
		result.TryGetValue(out Success<EmailAddress> success).ShouldBeTrue();
		success.Value.Masked.ShouldBe("j***@m***.uk");
	}

	[Fact]
	void Should_round_trip_through_try_parse_when_input_is_valid()
	{
		EmailAddress.TryParse("buvy@example.com", out var email).ShouldBeTrue();
		email.WireValue.ShouldBe("buvy@example.com");
	}

	[Fact]
	void Should_throw_when_default_instance_is_accessed()
	{
		EmailAddress malformed = default;
		Should.Throw<InvalidOperationException>(() => malformed.WireValue);
	}
}
