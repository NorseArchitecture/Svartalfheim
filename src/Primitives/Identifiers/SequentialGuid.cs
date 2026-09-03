using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Norse.Primitives.Identifiers;

/// <summary>
/// A guaranteed-well-formed RFC 9562 UUID version 7 value: time-ordered, safe to mint at any boundary
/// (including client-side, e.g. WASM/MAUI), and convertible to a byte order that sorts correctly under
/// SQL Server's <c>uniqueidentifier</c> comparison when a transactional table needs it.
/// </summary>
/// <remarks>
/// See <see cref="SequentialGuidBytes"/> for the byte-level layout and the SQL Server shuffle contract.
/// The public surface is deliberately narrow: no <see cref="object.ToString"/> override, no parsing, no
/// comparison operators (see the design doc's trust-boundary rationale, §3.1) — <see cref="CompareTo"/>
/// covers in-memory sorting and dictionary/EF-key use without widening the surface further. Untrusted
/// input always goes through <see cref="GuidParser"/>'s <see cref="Result{T}"/> gateway, never through
/// this type directly — the only supported construction paths are "generate a new one" and "wrap a
/// <see cref="Guid"/> this platform already produced."
/// </remarks>
[SuppressMessage("Design", "CA1036:Override methods on comparable types",
	Justification =
		"Deliberately narrow public surface (design doc §3.1): CompareTo covers in-memory sorting and EF-key comparisons; operator sugar is deferred until a concrete caller needs it.")]
public readonly record struct SequentialGuid : INorseGuid, IComparable<SequentialGuid>
{
	static int _counter = RandomNumberGenerator.GetInt32(0x200);

	/// <inheritdoc />
	public Guid Value { get; }

	/// <summary>Gets which byte layout <see cref="Value"/> is currently in.</summary>
	public GuidByteOrder Order { get; }

	/// <summary>Gets the UTC timestamp embedded in <see cref="Value"/>.</summary>
	public DateTime Timestamp { get; }

	/// <summary>Generates a new value from the current time. Always <see cref="GuidByteOrder.Rfc9562"/>.</summary>
	public SequentialGuid()
	{
		Value = NativeCapability.Available ? HyperUuid.UuidGenerator.NewV7() : GenerateManagedV7();
		Order = GuidByteOrder.Rfc9562;
		Timestamp = SequentialGuidBytes.ExtractTimestamp(Value, Order);
	}

	static Guid GenerateManagedV7()
	{
		var unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var counter = Interlocked.Increment(ref _counter) & 0x3FFFFFF;

		Span<byte> entropy = stackalloc byte[6];
		RandomNumberGenerator.Fill(entropy);

		return SequentialGuidBytes.GenerateRfc(unixMilliseconds, counter, entropy);
	}

	/// <summary>Wraps an existing value that this platform already produced, tagging it with its known byte order.</summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is <see cref="GuidByteOrder.Unspecified"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="value"/> is not a version 7 UUID with RFC 9562 variant bits.</exception>
	public SequentialGuid(Guid value, GuidByteOrder order)
	{
		if (order == GuidByteOrder.Unspecified)
			throw new ArgumentOutOfRangeException(nameof(order), order,
				"GuidByteOrder.Unspecified is never a valid argument.");
		if (!GuidVersionBits.HasVersionAndVariant(value, 7))
			throw new ArgumentException("Value must be a version 7 UUID with RFC 9562 variant bits.", nameof(value));

		Value = value;
		Order = order;
		Timestamp = SequentialGuidBytes.ExtractTimestamp(value, order);
	}

	/// <summary>Returns this value converted to <see cref="GuidByteOrder.SqlServer"/> order (a no-op if already there).</summary>
	/// <exception cref="InvalidOperationException"><see cref="Order"/> is <see cref="GuidByteOrder.Unspecified"/> -- <c>default(SequentialGuid)</c> is malformed by construction.</exception>
	public SequentialGuid ToSqlOrder() =>
		Order switch
		{
			GuidByteOrder.Unspecified => throw new InvalidOperationException(
				"default(SequentialGuid) is malformed by construction -- Order is Unspecified. Only wrap a value this platform already produced via the two-arg constructor, or generate a new one with SequentialGuid()."),
			GuidByteOrder.SqlServer => this,
			_ when NativeCapability.Available => new(HyperUuid.UuidGenerator.V7ToSqlOrder(Value), GuidByteOrder.SqlServer),
			_ => new(SequentialGuidBytes.ToSqlOrder(Value), GuidByteOrder.SqlServer)
		};

	/// <summary>Returns this value converted to <see cref="GuidByteOrder.Rfc9562"/> order (a no-op if already there).</summary>
	/// <exception cref="InvalidOperationException"><see cref="Order"/> is <see cref="GuidByteOrder.Unspecified"/> -- <c>default(SequentialGuid)</c> is malformed by construction.</exception>
	public SequentialGuid ToRfcOrder() =>
		Order switch
		{
			GuidByteOrder.Unspecified => throw new InvalidOperationException(
				"default(SequentialGuid) is malformed by construction -- Order is Unspecified. Only wrap a value this platform already produced via the two-arg constructor, or generate a new one with SequentialGuid()."),
			GuidByteOrder.Rfc9562 => this,
			_ when NativeCapability.Available => new(HyperUuid.UuidGenerator.V7FromSqlOrder(Value), GuidByteOrder.Rfc9562),
			_ => new(SequentialGuidBytes.ToRfcOrder(Value), GuidByteOrder.Rfc9562)
		};

	/// <summary>Implicitly unwraps to the underlying <see cref="Guid"/> (storage/wire representation).</summary>
	[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates",
		Justification =
			"Deliberately narrow public surface (design doc §3.1): Value is already the named accessor for the wrapped Guid; a ToGuid() synonym would add a member with no new capability.")]
	public static implicit operator Guid(SequentialGuid value) =>
		value.Value;

	/// <inheritdoc />
	public bool Equals(SequentialGuid other) =>
		ToRfcOrder().Value == other.ToRfcOrder().Value;

	/// <inheritdoc />
	public override int GetHashCode() =>
		ToRfcOrder().Value.GetHashCode();

	/// <inheritdoc />
	public int CompareTo(SequentialGuid other)
	{
		var normalizedOther = other.Order == Order ? other :
			Order == GuidByteOrder.SqlServer ? other.ToSqlOrder() :
			other.ToRfcOrder();

		return Order == GuidByteOrder.SqlServer
			? new SqlGuid(Value).CompareTo(new(normalizedOther.Value))
			: Value.CompareTo(normalizedOther.Value);
	}

	// Bytes, not items -- chosen to keep the per-chunk stackalloc small and safe regardless of
	// batch size. Drawing entropy per chunk rather than per item turns an N-syscall RNG cost
	// (measured ~848 ns/item, dominating Fill's total time) into a single draw every
	// EntropyChunkBytes / 6 items -- the batch is still capped at the 26-bit counter space, but
	// the entropy buffer never grows past this regardless of how large that batch gets.
	const int EntropyChunkBytes = 3072;

	/// <summary>
	/// Fills <paramref name="destination"/> with new values sharing a single current-time capture, each
	/// claiming a contiguous slot in the process-global counter. All <see cref="GuidByteOrder.Rfc9562"/>.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="destination"/> exceeds the 26-bit counter space (67,108,864).</exception>
	public static void Fill(Span<SequentialGuid> destination)
	{
		if (destination.Length > 0x400_0000)
			throw new ArgumentOutOfRangeException(nameof(destination),
				"Batch size must not exceed the 26-bit counter space (67,108,864).");
		if (destination.IsEmpty)
			return;

		if (NativeCapability.Available)
		{
			FillNative(destination);
			return;
		}

		FillManaged(destination);
	}

	static void FillNative(Span<SequentialGuid> destination)
	{
		Span<Guid> native = destination.Length <= 256 ? stackalloc Guid[destination.Length] : new Guid[destination.Length];
		HyperUuid.UuidGenerator.FillV7(native);
		for (var i = 0; i < destination.Length; i++)
			destination[i] = new SequentialGuid(native[i], GuidByteOrder.Rfc9562);
	}

	static void FillManaged(Span<SequentialGuid> destination)
	{
		var unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var count = destination.Length;
		var start = Interlocked.Add(ref _counter, count) - count + 1;

		Span<byte> entropyChunk = stackalloc byte[EntropyChunkBytes];
		var chunkItemCapacity = EntropyChunkBytes / 6;

		for (var offset = 0; offset < count; offset += chunkItemCapacity)
		{
			var chunkCount = Math.Min(chunkItemCapacity, count - offset);
			var chunk = entropyChunk[..(chunkCount * 6)];
			RandomNumberGenerator.Fill(chunk);

			for (var i = 0; i < chunkCount; i++)
			{
				var counter = (start + offset + i) & 0x3FFFFFF;
				var value = SequentialGuidBytes.GenerateRfc(unixMilliseconds, counter, chunk.Slice(i * 6, 6));
				destination[offset + i] = new SequentialGuid(value, GuidByteOrder.Rfc9562);
			}
		}
	}

	/// <summary>Creates an array of <paramref name="count"/> new values sharing a single current-time capture.</summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or exceeds the 26-bit counter space.</exception>
	public static SequentialGuid[] CreateMany(int count)
	{
		switch (count)
		{
			case < 0 or > 0x400_0000:
				throw new ArgumentOutOfRangeException(nameof(count),
					"Count must be between 0 and the 26-bit counter space (67,108,864).");
			case 0:
				return [];
		}

		var result = new SequentialGuid[count];
		Fill(result);
		return result;
	}
}
