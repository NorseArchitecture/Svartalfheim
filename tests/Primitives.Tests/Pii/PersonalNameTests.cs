using Norse.Primitives.Pii;

namespace Norse.Primitives.Tests.Pii;

public sealed class PersonalNameTests
{
	[Fact]
	void Should_parse_and_normalize_when_input_is_valid()
	{
		PersonalName.Parse("  Buvinghausen ").TryGetValue(out Success<PersonalName> success).ShouldBeTrue();
		success.Value.WireValue.ShouldBe("Buvinghausen");
		success.Value.Normalized.ShouldBe("BUVINGHAUSEN");
	}

	[Fact]
	void Should_apply_form_c_normalization_when_input_is_decomposed()
	{
		// "é" as 'e' + combining acute accent (decomposed, Form D)
		PersonalName.Parse("Réne").TryGetValue(out Success<PersonalName> success).ShouldBeTrue();
		success.Value.WireValue.ShouldBe("Réne");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	void Should_fail_with_empty_reason_when_input_is_blank(string input)
	{
		PersonalName.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
	}

	[Theory]
	[InlineData("123")]
	[InlineData("---")]
	[InlineData("tab\there")]
	void Should_fail_with_malformed_reason_when_shape_is_invalid(string input)
	{
		PersonalName.Parse(input).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_fail_with_malformed_reason_when_input_exceeds_max_length()
	{
		PersonalName.Parse(new string('a', 129)).TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Theory]
	[InlineData("Buvinghausen", "B.")]
	[InlineData("van der Berg", "V.")]
	[InlineData("Ólafsson", "Ó.")]
	void Should_mask_to_single_uppercased_initial_when_rendered(string input, string expected)
	{
		PersonalName.Parse(input).TryGetValue(out Success<PersonalName> success).ShouldBeTrue();
		success.Value.Masked.ShouldBe(expected);
		success.Value.ToMasked(new DateOnly(2026, 8, 3)).ShouldBe(expected);
		success.Value.ToString().ShouldBe(expected);
	}

	[Fact]
	void Should_throw_when_default_instance_is_accessed()
	{
		PersonalName malformed = default;
		Should.Throw<InvalidOperationException>(() => malformed.WireValue);
	}

	[Fact]
	void Should_fail_with_malformed_reason_rather_than_throw_when_input_has_an_unpaired_surrogate()
	{
		// Regression: an unpaired UTF-16 surrogate (e.g. a name truncated mid-astral-character by
		// an upstream substring or column-width limit) used to reach string.IsNormalized/Normalize
		// in Parse, both of which throw ArgumentException on a lone surrogate -- an unhandled
		// exception in a PII primitive on a trust boundary. HasValidShape now rejects it outright.
		PersonalName.Parse("A\uD800").TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}

	[Fact]
	void Should_fail_with_malformed_reason_when_input_contains_a_real_surrogate_pair()
	{
		// A genuinely valid astral-plane character (U+1F600 GRINNING FACE, a real high/low
		// surrogate pair) is ALSO rejected by the conservative fix. HasValidShape iterates UTF-16
		// code units, not full code points, so it can't distinguish a valid pair from a lone unit
		// without real pair-tracking logic -- this primitive deliberately doesn't support
		// astral-plane characters (a scope choice, not a bug).
		PersonalName.Parse("A\U0001F600").TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
	}
}
