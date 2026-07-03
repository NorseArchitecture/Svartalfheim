using System.Text;
using Norse.Primitives.Identifiers;

namespace Norse.Primitives.Tests.Identifiers;

public sealed class DeterministicGuidTests
{
	[Fact]
	void Should_produce_the_same_value_when_namespace_and_name_are_the_same()
	{
		var first = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, "example.com");
		var second = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, "example.com");

		first.ShouldBe(second);
	}

	[Fact]
	void Should_produce_a_different_value_when_the_name_differs()
	{
		var first = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, "example.com");
		var second = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, "example.org");

		first.ShouldNotBe(second);
	}

	[Fact]
	void Should_produce_a_different_value_when_the_namespace_differs()
	{
		var first = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, "example.com");
		var second = new DeterministicGuid(DeterministicGuid.Namespaces.Url, "example.com");

		first.ShouldNotBe(second);
	}

	[Fact]
	void Should_produce_the_same_value_from_string_char_span_and_byte_span_overloads()
	{
		const string Name = "example.com";
		var fromString = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, Name);
		var fromCharSpan = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, Name.AsSpan());
		var fromByteSpan = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, Encoding.UTF8.GetBytes(Name));

		fromString.ShouldBe(fromCharSpan);
		fromCharSpan.ShouldBe(fromByteSpan);
	}

	[Fact]
	void Should_be_well_formed_version5_when_generated()
	{
		var value = new DeterministicGuid(DeterministicGuid.Namespaces.Url, "https://example.com");

		GuidVersionBits.HasVersionAndVariant(value.Value, 5).ShouldBeTrue();
	}

	[Fact]
	void Should_match_the_known_rfc_9562_dns_namespace_value()
	{
		DeterministicGuid.Namespaces.Dns.ShouldBe(new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8"));
	}

	[Fact]
	void Should_throw_when_wrapped_value_is_not_a_version5_guid()
	{
		Should.Throw<ArgumentException>(() => new DeterministicGuid(Guid.NewGuid()));
	}

	[Fact]
	void Should_not_throw_when_wrapping_an_already_generated_value()
	{
		var generated = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, "example.com");

		Should.NotThrow(() => new DeterministicGuid(generated.Value));
	}

	[Fact]
	void Should_unwrap_implicitly_to_guid()
	{
		var value = new DeterministicGuid(DeterministicGuid.Namespaces.Dns, "example.com");

		Guid unwrapped = value;

		unwrapped.ShouldBe(value.Value);
	}
}
