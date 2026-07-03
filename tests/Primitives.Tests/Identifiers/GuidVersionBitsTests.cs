using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Tests.Identifiers;

public sealed class GuidVersionBitsTests
{
	[Fact]
	void Should_return_true_when_version_and_variant_match()
	{
		// Hand-built native-layout bytes: byte[7] top nibble = 7 (version), byte[8] top 2 bits = 10 (variant).
		var bytes = new byte[16];
		bytes[7] = 0x70;
		bytes[8] = 0x80;
		var value = new Guid(bytes);

		GuidVersionBits.HasVersionAndVariant(value, 7).ShouldBeTrue();
	}

	[Fact]
	void Should_return_false_when_version_does_not_match()
	{
		var bytes = new byte[16];
		bytes[7] = 0x50; // version 5, not 7
		bytes[8] = 0x80;
		var value = new Guid(bytes);

		GuidVersionBits.HasVersionAndVariant(value, 7).ShouldBeFalse();
	}

	[Fact]
	void Should_return_false_when_variant_bits_are_not_rfc9562()
	{
		var bytes = new byte[16];
		bytes[7] = 0x70;
		bytes[8] = 0x00; // variant bits 00, not the RFC 9562 10xxxxxx
		var value = new Guid(bytes);

		GuidVersionBits.HasVersionAndVariant(value, 7).ShouldBeFalse();
	}
}
