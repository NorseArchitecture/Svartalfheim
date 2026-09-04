using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;

namespace Norse.Primitives.Identifiers;

/// <summary>
/// A guaranteed-well-formed RFC 9562 UUID version 5 value, deterministically derived from a namespace
/// and a name via SHA-1 (RFC 9562 §5.5 / §A.4 mandates SHA-1 for name-based v5 identifiers — a
/// specification requirement, not a security primitive).
/// </summary>
/// <remarks>
/// Exists so a lookup/reference-table foreign key can be computed in memory from a namespace + natural
/// key, without a database round trip. No <c>Timestamp</c>, no byte-order concept — a content hash has
/// no time component and no meaningful sort order.
/// </remarks>
[SuppressMessage("Design", "CA1036:Override methods on comparable types",
	Justification = "Deliberately narrow public surface (design doc §3.1): CompareTo covers in-memory sorting and EF-key comparisons; operator sugar is deferred until a concrete caller needs it.")]
public readonly record struct DeterministicGuid : INorseGuid, IComparable<DeterministicGuid>
{
	const int StackThreshold = 256;

	/// <summary>RFC 9562 §6.6 well-known namespace UUIDs.</summary>
	public static class Namespaces
	{
		/// <summary>Name string is a fully-qualified domain name.</summary>
		public static readonly Guid Dns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

		/// <summary>Name string is a URL.</summary>
		public static readonly Guid Url = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

		/// <summary>Name string is an ISO OID.</summary>
		public static readonly Guid Oid = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

		/// <summary>Name string is an X.500 DN (in DER or a text output format).</summary>
		public static readonly Guid X500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");
	}

	/// <inheritdoc />
	public Guid Value { get; }

	/// <summary>Derives a new value from <paramref name="namespaceId"/> and <paramref name="name"/>.</summary>
	public DeterministicGuid(Guid namespaceId, string name) : this(namespaceId, name.AsSpan()) { }

	/// <summary>Derives a new value from <paramref name="namespaceId"/> and <paramref name="name"/>.</summary>
	[SkipLocalsInit]
	public DeterministicGuid(Guid namespaceId, ReadOnlySpan<char> name)
	{
		if (NativeCapability.Available)
		{
			Value = HyperUuid.UuidGenerator.NewV5(namespaceId, name.ToString());
			return;
		}

		var maxByteCount = checked(16 + Encoding.UTF8.GetMaxByteCount(name.Length));
		Span<byte> stackBuffer = stackalloc byte[StackThreshold];
		var buffer = maxByteCount <= StackThreshold ? stackBuffer[..maxByteCount] : new byte[maxByteCount];
		WriteNamespace(namespaceId, buffer);
		var nameByteLength = Encoding.UTF8.GetBytes(name, buffer[16..]);
		Value = HashAndFinalize(buffer[..(16 + nameByteLength)]);
	}

	/// <summary>Derives a new value from <paramref name="namespaceId"/> and raw <paramref name="name"/> bytes.</summary>
	/// <remarks>
	/// The native path only applies when <paramref name="name"/> is valid UTF-8 -- HyperUuid's API
	/// only accepts <see cref="string"/>, so routing non-UTF-8 bytes through
	/// <see cref="Encoding.UTF8"/>'s <c>GetString</c> would lossily replace invalid sequences with
	/// U+FFFD before hashing, producing a different value than the managed path hashes from the
	/// identical raw bytes. Non-UTF-8 input always falls through to the managed hash below, on
	/// every platform, so the result is stable regardless of which engine happens to be available.
	/// </remarks>
	[SkipLocalsInit]
	public DeterministicGuid(Guid namespaceId, ReadOnlySpan<byte> name)
	{
		if (NativeCapability.Available && Utf8.IsValid(name))
		{
			Value = HyperUuid.UuidGenerator.NewV5(namespaceId, Encoding.UTF8.GetString(name));
			return;
		}

		var totalLength = checked(16 + name.Length);
		Span<byte> stackBuffer = stackalloc byte[StackThreshold];
		var buffer = totalLength <= StackThreshold ? stackBuffer[..totalLength] : new byte[totalLength];
		WriteNamespace(namespaceId, buffer);
		name.CopyTo(buffer[16..]);
		Value = HashAndFinalize(buffer);
	}

	/// <summary>Wraps an already-computed value.</summary>
	/// <exception cref="ArgumentException"><paramref name="value"/> is not a version 5 UUID with RFC 9562 variant bits.</exception>
	public DeterministicGuid(Guid value)
	{
		if (!GuidVersionBits.HasVersionAndVariant(value, 5))
			throw new ArgumentException("Value must be a version 5 UUID with RFC 9562 variant bits.", nameof(value));

		Value = value;
	}

	static void WriteNamespace(Guid namespaceId, Span<byte> destination) =>
		namespaceId.TryWriteBytes(destination[..16], bigEndian: true, out _);

	[SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
		Justification = "RFC 9562 §A.4 mandates SHA-1 for UUIDv5 name-based identifiers; this is a specification requirement, not a security primitive.")]
	static Guid HashAndFinalize(ReadOnlySpan<byte> input)
	{
		Span<byte> digest = stackalloc byte[20];
		SHA1.HashData(input, digest);

		var head = digest[..16];
		head[6] = (byte)((head[6] & 0x0F) | (5 << 4));
		head[8] = (byte)((head[8] & 0x3F) | 0x80);

		return new Guid(head, bigEndian: true);
	}

	/// <summary>Implicitly unwraps to the underlying <see cref="Guid"/> (storage/wire representation).</summary>
	[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates",
		Justification = "Deliberately narrow public surface (design doc §3.1): Value is already the named accessor for the wrapped Guid; a ToGuid() synonym would add a member with no new capability.")]
	public static implicit operator Guid(DeterministicGuid value) =>
		value.Value;

	/// <inheritdoc />
	public bool Equals(DeterministicGuid other) =>
		Value.Equals(other.Value);

	/// <inheritdoc />
	public override int GetHashCode() =>
		Value.GetHashCode();

	/// <inheritdoc />
	public int CompareTo(DeterministicGuid other) =>
		Value.CompareTo(other.Value);
}
