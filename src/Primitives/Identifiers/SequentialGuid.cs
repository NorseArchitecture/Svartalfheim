using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using HyperUuid;

namespace Norse.Primitives.Identifiers;

/// <summary>
/// A guaranteed-well-formed RFC 9562 UUID version 7 value: time-ordered, safe to mint at any boundary
/// (including client-side, e.g. WASM/MAUI), and convertible to a byte order that sorts correctly under
/// SQL Server's <c>uniqueidentifier</c> comparison when a transactional table needs it.
/// </summary>
/// <remarks>
/// The engine is HyperUuid's <see cref="UuidGenerator"/>: generation, the SQL Server permutation (pinned
/// byte for byte by HyperUuid's <c>corpus/sql_order.json</c>, which was generated from this realm's own
/// retired arithmetic), timestamp extraction, and version/variant inspection — all layout-aware, so a
/// SQL-ordered value is inspected in place. This type is the forge's contract over that engine:
/// <see cref="GuidByteOrder"/> is <c>UuidLayout</c> by number.
/// The public surface is deliberately narrow: no <see cref="object.ToString"/> override, no parsing, no
/// comparison operators (see the design doc's trust-boundary rationale, §3.1) — <see cref="CompareTo"/>
/// covers in-memory sorting and dictionary/EF-key use without widening the surface further. Untrusted
/// input always goes through the <see cref="Parser"/> gateway's <see cref="Result{T}"/>, never through
/// this type directly — the only supported construction paths are "generate a new one" and "wrap a
/// <see cref="Guid"/> this platform already produced."
/// </remarks>
[SuppressMessage("Design", "CA1036:Override methods on comparable types",
	Justification =
		"Deliberately narrow public surface (design doc §3.1): CompareTo covers in-memory sorting and EF-key comparisons; operator sugar is deferred until a concrete caller needs it.")]
public readonly record struct SequentialGuid : INorseGuid, IComparable<SequentialGuid>
{
	/// <summary>Values the engine fills per native call while a batch is wrapped: 4 KB of stack.</summary>
	const int FillChunk = 256;

	/// <inheritdoc />
	public Guid Value { get; }

	/// <summary>Gets which byte layout <see cref="Value"/> is currently in.</summary>
	public GuidByteOrder Order { get; }

	/// <summary>Gets the UTC timestamp embedded in <see cref="Value"/>.</summary>
	public DateTime Timestamp { get; }

	/// <summary>Generates a new value from the current time. Always <see cref="GuidByteOrder.Rfc9562"/>.</summary>
	public SequentialGuid() : this(UuidGenerator.NewV7(), GuidByteOrder.Rfc9562)
	{
	}

	/// <summary>Wraps an existing value that this platform already produced, tagging it with its known byte order.</summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is <see cref="GuidByteOrder.Unspecified"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="value"/> is not a version 7 UUID with RFC 9562 variant bits.</exception>
	public SequentialGuid(Guid value, GuidByteOrder order)
	{
		if (order == GuidByteOrder.Unspecified)
			throw new ArgumentOutOfRangeException(nameof(order), order,
				"GuidByteOrder.Unspecified is never a valid argument.");
		// Layout-aware: a SQL-ordered value is inspected where its version and variant actually sit.
		if (!UuidGenerator.IsRfc(value, 7, (UuidLayout)order))
			throw new ArgumentException("Value must be a version 7 UUID with RFC 9562 variant bits.", nameof(value));

		Value = value;
		Order = order;
		Timestamp = UuidGenerator.V7Timestamp(value, (UuidLayout)order).UtcDateTime;
	}

	/// <summary>Returns this value converted to <see cref="GuidByteOrder.SqlServer"/> order (a no-op if already there).</summary>
	/// <exception cref="InvalidOperationException"><see cref="Order"/> is <see cref="GuidByteOrder.Unspecified"/> -- <c>default(SequentialGuid)</c> is malformed by construction.</exception>
	public SequentialGuid ToSqlOrder() =>
		Order switch
		{
			GuidByteOrder.Unspecified => throw new InvalidOperationException(
				"default(SequentialGuid) is malformed by construction -- Order is Unspecified. Only wrap a value this platform already produced via the two-arg constructor, or generate a new one with SequentialGuid()."),
			GuidByteOrder.SqlServer => this,
			_ => new(UuidGenerator.V7ToSqlOrder(Value), GuidByteOrder.SqlServer)
		};

	/// <summary>Returns this value converted to <see cref="GuidByteOrder.Rfc9562"/> order (a no-op if already there).</summary>
	/// <exception cref="InvalidOperationException"><see cref="Order"/> is <see cref="GuidByteOrder.Unspecified"/> -- <c>default(SequentialGuid)</c> is malformed by construction.</exception>
	public SequentialGuid ToRfcOrder() =>
		Order switch
		{
			GuidByteOrder.Unspecified => throw new InvalidOperationException(
				"default(SequentialGuid) is malformed by construction -- Order is Unspecified. Only wrap a value this platform already produced via the two-arg constructor, or generate a new one with SequentialGuid()."),
			GuidByteOrder.Rfc9562 => this,
			_ => new(UuidGenerator.V7FromSqlOrder(Value), GuidByteOrder.Rfc9562)
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

	/// <summary>
	/// Fills <paramref name="destination"/> with new values sharing a single current-time capture, each
	/// claiming a contiguous slot in the engine's counter. All <see cref="GuidByteOrder.Rfc9562"/>.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="destination"/> exceeds the 26-bit counter space (67,108,864).</exception>
	public static void Fill(Span<SequentialGuid> destination)
	{
		if (destination.Length > UuidGenerator.MaxV7Batch)
			throw new ArgumentOutOfRangeException(nameof(destination),
				"Batch size must not exceed the 26-bit counter space (67,108,864).");
		if (destination.IsEmpty)
			return;

		// One timestamp for the whole batch, as before; the engine's counter carries across the
		// chunks and rolls the millisecond forward itself if it ever wraps.
		var unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		Span<Guid> scratch = stackalloc Guid[Math.Min(destination.Length, FillChunk)];
		for (var offset = 0; offset < destination.Length; offset += scratch.Length)
		{
			var chunk = scratch[..Math.Min(scratch.Length, destination.Length - offset)];
			UuidGenerator.FillV7(chunk, unixMilliseconds);
			for (var i = 0; i < chunk.Length; i++)
				destination[offset + i] = new(chunk[i], GuidByteOrder.Rfc9562);
		}
	}

	/// <summary>Creates an array of <paramref name="count"/> new values sharing a single current-time capture.</summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or exceeds the 26-bit counter space.</exception>
	public static SequentialGuid[] CreateMany(int count)
	{
		switch (count)
		{
			case < 0 or > UuidGenerator.MaxV7Batch:
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
