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
	Justification = "Deliberately narrow public surface (design doc §3.1): CompareTo covers in-memory " +
		"sorting and EF-key comparisons; operator sugar is deferred until a concrete caller needs it.")]
public readonly record struct SequentialGuid : INorseGuid, IComparable<SequentialGuid>, IEquatable<SequentialGuid>
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
		var unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var counter = Interlocked.Increment(ref _counter) & 0x3FFFFFF;

		Span<byte> entropy = stackalloc byte[6];
		RandomNumberGenerator.Fill(entropy);

		Value = SequentialGuidBytes.GenerateRfc(unixMilliseconds, counter, entropy);
		Order = GuidByteOrder.Rfc9562;
		Timestamp = SequentialGuidBytes.ExtractTimestamp(Value, Order);
	}

	/// <summary>Wraps an existing value that this platform already produced, tagging it with its known byte order.</summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is <see cref="GuidByteOrder.Unspecified"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="value"/> is not a version 7 UUID with RFC 9562 variant bits.</exception>
	public SequentialGuid(Guid value, GuidByteOrder order)
	{
		if (order == GuidByteOrder.Unspecified)
			throw new ArgumentOutOfRangeException(nameof(order), order, "GuidByteOrder.Unspecified is never a valid argument.");
		if (!GuidVersionBits.HasVersionAndVariant(value, 7))
			throw new ArgumentException("Value must be a version 7 UUID with RFC 9562 variant bits.", nameof(value));

		Value = value;
		Order = order;
		Timestamp = SequentialGuidBytes.ExtractTimestamp(value, order);
	}

	/// <summary>Returns this value converted to <see cref="GuidByteOrder.SqlServer"/> order (a no-op if already there).</summary>
	public SequentialGuid ToSqlOrder() =>
		Order == GuidByteOrder.SqlServer ? this : new SequentialGuid(SequentialGuidBytes.ToSqlOrder(Value), GuidByteOrder.SqlServer);

	/// <summary>Returns this value converted to <see cref="GuidByteOrder.Rfc9562"/> order (a no-op if already there).</summary>
	public SequentialGuid ToRfcOrder() =>
		Order == GuidByteOrder.Rfc9562 ? this : new SequentialGuid(SequentialGuidBytes.ToRfcOrder(Value), GuidByteOrder.Rfc9562);

	/// <summary>Implicitly unwraps to the underlying <see cref="Guid"/> (storage/wire representation).</summary>
	[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates",
		Justification = "Deliberately narrow public surface (design doc §3.1): Value is already the " +
			"named accessor for the wrapped Guid; a ToGuid() synonym would add a member with no new capability.")]
	public static implicit operator Guid(SequentialGuid value) => value.Value;

	/// <inheritdoc />
	public bool Equals(SequentialGuid other) =>
		ToRfcOrder().Value == other.ToRfcOrder().Value;

	/// <inheritdoc />
	public override int GetHashCode() =>
		ToRfcOrder().Value.GetHashCode();

	/// <inheritdoc />
	public int CompareTo(SequentialGuid other)
	{
		var normalizedOther = other.Order == Order
			? other
			: Order == GuidByteOrder.SqlServer ? other.ToSqlOrder() : other.ToRfcOrder();

		return Order == GuidByteOrder.SqlServer
			? new SqlGuid(Value).CompareTo(new SqlGuid(normalizedOther.Value))
			: Value.CompareTo(normalizedOther.Value);
	}
}
