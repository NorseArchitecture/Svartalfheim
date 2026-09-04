using System.Text;
using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Tests.Identifiers;

public sealed class DeterministicGuidTests
{
	[Fact]
	void Should_produce_the_same_value_when_namespace_and_name_are_the_same()
	{
		DeterministicGuid
			first = new(DeterministicGuid.Namespaces.Dns, "example.com"),
			second = new(DeterministicGuid.Namespaces.Dns, "example.com");

		first.ShouldBe(second);
	}

	[Fact]
	void Should_produce_a_different_value_when_the_name_differs()
	{
		DeterministicGuid
			first = new(DeterministicGuid.Namespaces.Dns, "example.com"),
			second = new(DeterministicGuid.Namespaces.Dns, "example.org");

		first.ShouldNotBe(second);
	}

	[Fact]
	void Should_produce_a_different_value_when_the_namespace_differs()
	{
		DeterministicGuid
			first = new(DeterministicGuid.Namespaces.Dns, "example.com"),
			second = new(DeterministicGuid.Namespaces.Url, "example.com");

		first.ShouldNotBe(second);
	}

	[Fact]
	void Should_produce_the_same_value_from_string_char_span_and_byte_span_overloads()
	{
		const string Name = "example.com";
		DeterministicGuid
			fromString = new(DeterministicGuid.Namespaces.Dns, Name),
			fromCharSpan = new(DeterministicGuid.Namespaces.Dns, Name.AsSpan()),
			fromByteSpan = new(DeterministicGuid.Namespaces.Dns, Encoding.UTF8.GetBytes(Name));

		fromString.ShouldBe(fromCharSpan);
		fromCharSpan.ShouldBe(fromByteSpan);
	}

	[Fact]
	void Should_be_well_formed_version5_when_generated()
	{
		DeterministicGuid value = new(DeterministicGuid.Namespaces.Url, "https://example.com");
		GuidVersionBits.HasVersionAndVariant(value.Value, 5).ShouldBeTrue();
	}

	[Fact]
	void Should_match_the_known_rfc_9562_dns_namespace_value() =>
		DeterministicGuid.Namespaces.Dns.ShouldBe(new("6ba7b810-9dad-11d1-80b4-00c04fd430c8"));

	[Fact]
	void Should_throw_when_wrapped_value_is_not_a_version5_guid() =>
		Should.Throw<ArgumentException>(() => new DeterministicGuid(Guid.NewGuid()));

	[Fact]
	void Should_not_throw_when_wrapping_an_already_generated_value()
	{
		DeterministicGuid generated = new(DeterministicGuid.Namespaces.Dns, "example.com");
		Should.NotThrow(() => new DeterministicGuid(generated.Value));
	}

	[Fact]
	void Should_unwrap_implicitly_to_guid()
	{
		DeterministicGuid value = new(DeterministicGuid.Namespaces.Dns, "example.com");
		Guid unwrapped = value;
		unwrapped.ShouldBe(value.Value);
	}

	[Fact]
	void Native_and_managed_paths_produce_the_identical_value_for_the_same_input()
	{
		var namespaceId = DeterministicGuid.Namespaces.Dns;
		const string Name = "example.com";

		var native = new DeterministicGuid(namespaceId, Name);

		DeterministicGuid managed = default;
		NativeCapability.ForManagedOnly(() =>
			managed = new DeterministicGuid(namespaceId, Name));

		native.Value.ShouldBe(managed.Value);
	}

	[Fact]
	void Native_and_managed_paths_produce_the_identical_value_for_a_lone_continuation_byte() =>
		AssertNativeAndManagedAgreeOnInvalidUtf8([0x80]);

	[Fact]
	void Native_and_managed_paths_produce_the_identical_value_for_an_unpaired_byte_order_mark_sequence() =>
		AssertNativeAndManagedAgreeOnInvalidUtf8([0xFF, 0xFE, 0x00, 0x01]);

	[Fact]
	void Native_and_managed_paths_produce_the_identical_value_for_an_invalid_two_byte_sequence() =>
		AssertNativeAndManagedAgreeOnInvalidUtf8([0xC3, 0x28]);

	// Regression: the byte-span overload's native branch used to route ANY byte input through
	// Encoding.UTF8.GetString before handing it to HyperUuid -- for bytes that aren't valid UTF-8
	// (a raw hash, a protobuf payload, an encrypted blob), that round-trip is lossy (invalid
	// sequences become U+FFFD), producing a DIFFERENT v5 value than the managed path hashes from
	// the identical raw bytes. The native branch now only applies when the bytes are valid UTF-8;
	// invalid-UTF-8 input always falls through to the managed hash, on every platform, so the two
	// paths must agree here regardless of which engine is available.
	static void AssertNativeAndManagedAgreeOnInvalidUtf8(byte[] name)
	{
		var namespaceId = DeterministicGuid.Namespaces.Dns;

		var native = new DeterministicGuid(namespaceId, (ReadOnlySpan<byte>)name);

		DeterministicGuid managed = default;
		NativeCapability.ForManagedOnly(() =>
			managed = new DeterministicGuid(namespaceId, (ReadOnlySpan<byte>)name));

		native.Value.ShouldBe(managed.Value);
	}
}
