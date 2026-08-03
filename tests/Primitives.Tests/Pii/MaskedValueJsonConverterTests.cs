using System.Text.Json;

namespace Norse.Primitives.Tests.Pii;

public sealed class MaskedValueJsonConverterTests
{
	// Local fixture type: the converter is generic over any IMaskedValue struct.
	readonly record struct FakePii(string Secret) : IMaskedValue
	{
		public string Masked => "***";
		public string ToMasked(DateOnly asOf) => Masked;
	}

	static readonly JsonSerializerOptions _options = BuildOptions();

	static JsonSerializerOptions BuildOptions()
	{
		JsonSerializerOptions options = new();
		options.Converters.Add(new MaskedValueJsonConverter<FakePii>());
		return options;
	}

	[Fact]
	void Should_write_the_masked_value_when_serialized() =>
		JsonSerializer.Serialize(new FakePii("buvy@example.com"), _options).ShouldBe("\"***\"");

	[Fact]
	void Should_throw_when_deserialization_is_attempted() =>
		Should.Throw<NotSupportedException>(() => JsonSerializer.Deserialize<FakePii>("\"***\"", _options));
}
