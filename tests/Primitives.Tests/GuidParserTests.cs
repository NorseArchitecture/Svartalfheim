namespace Norse.Primitives.Tests;

public sealed class GuidParserTests
{
	const string Known = "01020304-0506-0708-090a-0b0c0d0e0f10";

	static readonly Guid _expected = new(Known);

	[Theory]
	[InlineData("01020304-0506-0708-090a-0b0c0d0e0f10")]              // D
	[InlineData("0102030405060708090a0b0c0d0e0f10")]                  // N
	[InlineData("{01020304-0506-0708-090a-0b0c0d0e0f10}")]            // B
	[InlineData("(01020304-0506-0708-090a-0b0c0d0e0f10)")]            // P
	[InlineData("  01020304-0506-0708-090a-0b0c0d0e0f10  ")]          // surrounding whitespace
	[InlineData("urn:uuid:01020304-0506-0708-090a-0b0c0d0e0f10")]     // URN prefix
	[InlineData("GUID:01020304-0506-0708-090a-0b0c0d0e0f10")]         // GUID: prefix
	[InlineData("uuid:01020304-0506-0708-090a-0b0c0d0e0f10")]         // case-insensitive UUID:
	void Should_parse_value_when_guid_input_is_recognized(string input)
	{
		var actual = GuidParser.ParseRequired(input);
		actual.TryGetValue(out Success<Guid> success).ShouldBeTrue();
		success.Value.ShouldBe(_expected);
	}

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
		var actual = GuidParser.ParseOptional("urn:uuid:" + Known);
		actual.HasValue.ShouldBeTrue();
		actual.Value.TryGetValue(out Success<Guid> success).ShouldBeTrue();
		success.Value.ShouldBe(_expected);
	}
}
