namespace Norse.Primitives.Tests;

// Runs in NativeCapabilityCollection: the "_on_the_forced_managed_path" theories/facts below call
// NativeCapability.ForManagedOnly, which mutates thread-local state that must not race another
// test reading NativeCapability.Available concurrently.
[Collection(nameof(NativeCapabilityCollection))]
public sealed class GuidParserTests
{
	const string Known = "01020304-0506-0708-090a-0b0c0d0e0f10";

	static readonly Guid _expected = new(Known);

	public static TheoryData<string> RecognizedInputs =>
	[
		"01020304-0506-0708-090a-0b0c0d0e0f10",              // D
		"0102030405060708090a0b0c0d0e0f10",                  // N
		"{01020304-0506-0708-090a-0b0c0d0e0f10}",            // B
		"(01020304-0506-0708-090a-0b0c0d0e0f10)",            // P
		"  01020304-0506-0708-090a-0b0c0d0e0f10  ",          // surrounding whitespace
		"urn:uuid:01020304-0506-0708-090a-0b0c0d0e0f10",     // URN prefix
		"GUID:01020304-0506-0708-090a-0b0c0d0e0f10",         // GUID: prefix
		"uuid:01020304-0506-0708-090a-0b0c0d0e0f10",         // case-insensitive UUID:
	];

	[Theory]
	[MemberData(nameof(RecognizedInputs))]
	void Should_parse_value_when_guid_input_is_recognized(string input)
	{
		var actual = GuidParser.ParseRequired(input);
		actual.TryGetValue(out Success<Guid> success).ShouldBeTrue();
		success.Value.ShouldBe(_expected);
	}

	[Theory]
	[MemberData(nameof(RecognizedInputs))]
	void Should_parse_value_when_guid_input_is_recognized_on_the_forced_managed_path(string input) =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = GuidParser.ParseRequired(input);
			actual.TryGetValue(out Success<Guid> success).ShouldBeTrue();
			success.Value.ShouldBe(_expected);
		});

	[Theory]
	[InlineData("not-a-guid")]
	[InlineData("GUID:not-a-guid")]
	[InlineData("01020304-0506-0708-090a-0b0c0d0e0f10-extra")]
	void Should_fail_with_malformed_reason_when_guid_input_is_unrecognized(string input)
	{
		var actual = GuidParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Malformed);
		failure.ExpectedType.ShouldBe("Guid");
		failure.Format.ShouldBeNull();
		failure.Detail.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	void Should_fail_with_empty_reason_when_required_input_is_absent(string? input)
	{
		var actual = GuidParser.ParseRequired(input);
		actual.TryGetValue(out Failure failure).ShouldBeTrue();
		failure.Reason.ShouldBe(ParseFailure.Empty);
		failure.Input.ShouldBe(string.Empty);
		failure.ExpectedType.ShouldBe("Guid");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	void Should_return_absent_when_optional_input_is_absent(string? input) =>
		GuidParser.ParseOptional(input).HasValue.ShouldBeFalse();

	[Fact]
	void Should_parse_value_when_optional_input_is_recognized()
	{
		var actual = GuidParser.ParseOptional($"urn:uuid:{Known}");
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<Guid> success).ShouldBeTrue();
		success.Value.ShouldBe(_expected);
	}

	[Fact]
	void Should_parse_value_when_optional_input_is_recognized_on_the_forced_managed_path() =>
		NativeCapability.ForManagedOnly(() =>
		{
			var actual = GuidParser.ParseOptional($"urn:uuid:{Known}");
			actual.HasValue.ShouldBeTrue();
			actual.Value.TryGetValue(out Success<Guid> success).ShouldBeTrue();
			success.Value.ShouldBe(_expected);
		});
}
