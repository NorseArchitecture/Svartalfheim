using Norse.Primitives.Pii;

namespace Norse.Primitives.Tests.Pii;

public sealed class PhoneNumberTests
{
	[Theory]
	[InlineData("+15551234567", "+15551234567")]
	[InlineData("+1 (555) 123-4567", "+15551234567")]
	[InlineData("+44 20.7946.0958", "+442079460958")]
	void Should_canonicalize_to_e164_when_input_carries_separators(string input, string expected)
	{
		PhoneNumber.Parse(input).TryGetValue(out Success<PhoneNumber> success).ShouldBeTrue();
		success.Value.WireValue.ShouldBe(expected);
		success.Value.Normalized.ShouldBe(expected);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	void Should_fail_with_empty_reason_when_input_is_blank(string input)
	{
		PhoneNumber.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
	}

	[Theory]
	[InlineData("5551234567")]        // no leading +
	[InlineData("+05551234567")]      // leading zero country code
	[InlineData("+1234567")]          // 7 digits — below floor
	[InlineData("+1234567890123456")] // 16 digits — above E.164 max
	[InlineData("+1555ABC4567")]      // letters
	void Should_fail_with_malformed_reason_when_shape_is_invalid(string input)
	{
		PhoneNumber.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_mask_to_last_four_digits_when_rendered()
	{
		PhoneNumber.Parse("+15551234567").TryGetValue(out Success<PhoneNumber> success).ShouldBeTrue();
		success.Value.Masked.ShouldBe("***4567");
		success.Value.ToMasked(new DateOnly(2026, 8, 3)).ShouldBe("***4567");
		success.Value.ToString().ShouldBe("***4567");
	}

	[Fact]
	void Should_throw_when_default_instance_is_accessed()
	{
		PhoneNumber malformed = default;
		Should.Throw<InvalidOperationException>(() => malformed.WireValue);
	}
}
