using System.Runtime.CompilerServices;

namespace Norse.Primitives.Identifiers;

#pragma warning disable CS1574
/// <summary>
/// RFC 9562 UUID version 7 byte-level generation and the bidirectional SQL Server byte-order shuffle
/// that <see cref="SequentialGuid"/> wraps. Kept separate from the public struct so the byte math can be
/// exercised directly by its own correctness-oracle tests without going through timestamp/RNG capture.
/// </summary>
/// <remarks>
/// <para>
/// RFC 9562 layout (16 bytes): <c>[0..6)</c> unix_ts_ms (48 bits, big-endian) · <c>[6]</c> version nibble
/// (top) + counter-high nibble (bottom) · <c>[7]</c> counter-high low byte (rand_a = 12-bit counter chunk)
/// · <c>[8]</c> variant bits (top 2) + counter-low top 6 bits · <c>[9]</c> counter-low low byte
/// (rand_b-start = 14-bit counter chunk) · <c>[10..16)</c> entropy (48 bits, random).
/// </para>
/// </remarks>
#pragma warning restore CS1574
static class SequentialGuidBytes
{
	/// <summary>
	/// Builds a new RFC 9562-ordered UUID version 7 from an explicit timestamp, counter, and entropy —
	/// no clock or RNG access, so callers (and tests) can pin every input.
	/// </summary>
	/// <param name="unixMilliseconds">Milliseconds since the Unix epoch; must fit in 48 bits.</param>
	/// <param name="counter">The monotonic counter value; only the low 26 bits are used.</param>
	/// <param name="entropy">Exactly 6 bytes of random tail.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="unixMilliseconds"/> is negative or exceeds 48 bits.</exception>
	/// <exception cref="ArgumentException"><paramref name="entropy"/> is not exactly 6 bytes.</exception>
	[SkipLocalsInit]
	internal static Guid GenerateRfc(long unixMilliseconds, int counter, ReadOnlySpan<byte> entropy)
	{
		if (unixMilliseconds is < 0 or > 0x0000_FFFF_FFFF_FFFF)
			throw new ArgumentOutOfRangeException(nameof(unixMilliseconds),
				"Unix millisecond timestamp must be non-negative and fit within 48 bits.");
		if (entropy.Length != 6)
			throw new ArgumentException("Entropy must be exactly 6 bytes.", nameof(entropy));

		var maskedCounter = counter & 0x3FFFFFF;

		Span<byte> bytes = stackalloc byte[16];
		entropy.CopyTo(bytes[10..]);

		bytes[0] = (byte)(unixMilliseconds >> 40);
		bytes[1] = (byte)(unixMilliseconds >> 32);
		bytes[2] = (byte)(unixMilliseconds >> 24);
		bytes[3] = (byte)(unixMilliseconds >> 16);
		bytes[4] = (byte)(unixMilliseconds >> 8);
		bytes[5] = (byte)unixMilliseconds;

		bytes[6] = (byte)(maskedCounter >> 22);
		bytes[7] = (byte)((maskedCounter >> 14) & 0xFF);
		bytes[8] = (byte)((maskedCounter >> 8) & 0x3F);
		bytes[9] = (byte)(maskedCounter & 0xFF);

		bytes[6] = (byte)((bytes[6] & 0x0F) | (7 << 4));
		bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

		return new Guid(bytes, bigEndian: true);
	}

	/// <summary>Converts an <see cref="GuidByteOrder.Rfc9562"/>-ordered value to <see cref="GuidByteOrder.SqlServer"/> order.</summary>
	[SkipLocalsInit]
	internal static Guid ToSqlOrder(Guid rfcOrdered)
	{
		Span<byte> native = stackalloc byte[16];
		rfcOrdered.TryWriteBytes(native);

		var counterHi = ((native[7] & 0x0F) << 8) | native[6];
		var counterLo = ((native[8] & 0x3F) << 8) | native[9];
		var counter = (counterHi << 14) | counterLo;

		var top14 = (counter >> 12) & 0x3FFF;
		var bottom12 = counter & 0xFFF;

		var version = (byte)(native[7] & 0xF0);
		var variant = (byte)(native[8] & 0xC0);

		Span<byte> sql = stackalloc byte[16];
		sql[10] = native[3];
		sql[11] = native[2];
		sql[12] = native[1];
		sql[13] = native[0];
		sql[14] = native[5];
		sql[15] = native[4];
		sql[8] = (byte)(variant | ((top14 >> 8) & 0x3F));
		sql[9] = (byte)(top14 & 0xFF);
		sql[7] = (byte)(version | ((bottom12 >> 8) & 0x0F));
		sql[6] = (byte)(bottom12 & 0xFF);
		sql[4] = native[10];
		sql[5] = native[11];
		sql[0] = native[12];
		sql[1] = native[13];
		sql[2] = native[14];
		sql[3] = native[15];

		return new Guid(sql);
	}

	/// <summary>Converts a <see cref="GuidByteOrder.SqlServer"/>-ordered value back to <see cref="GuidByteOrder.Rfc9562"/> order.</summary>
	[SkipLocalsInit]
	internal static Guid ToRfcOrder(Guid sqlOrdered)
	{
		Span<byte> sql = stackalloc byte[16];
		sqlOrdered.TryWriteBytes(sql);

		var top14 = ((sql[8] & 0x3F) << 8) | sql[9];
		var bottom12 = ((sql[7] & 0x0F) << 8) | sql[6];
		var counter = (top14 << 12) | bottom12;

		var counterHi = (counter >> 14) & 0xFFF;
		var counterLo = counter & 0x3FFF;

		var version = (byte)(sql[7] & 0xF0);
		var variant = (byte)(sql[8] & 0xC0);

		Span<byte> native = stackalloc byte[16];
		native[3] = sql[10];
		native[2] = sql[11];
		native[1] = sql[12];
		native[0] = sql[13];
		native[5] = sql[14];
		native[4] = sql[15];
		native[6] = (byte)(counterHi & 0xFF);
		native[7] = (byte)(version | ((counterHi >> 8) & 0x0F));
		native[8] = (byte)(variant | ((counterLo >> 8) & 0x3F));
		native[9] = (byte)(counterLo & 0xFF);
		native[10] = sql[4];
		native[11] = sql[5];
		native[12] = sql[0];
		native[13] = sql[1];
		native[14] = sql[2];
		native[15] = sql[3];

		return new Guid(native);
	}

	/// <summary>
	/// Extracts the embedded 48-bit Unix millisecond timestamp, normalizing to RFC order first if
	/// <paramref name="order"/> is <see cref="GuidByteOrder.SqlServer"/>.
	/// </summary>
	internal static DateTime ExtractTimestamp(Guid value, GuidByteOrder order)
	{
		var rfcValue = order == GuidByteOrder.SqlServer ? ToRfcOrder(value) : value;

		Span<byte> native = stackalloc byte[16];
		rfcValue.TryWriteBytes(native);

		var unixMilliseconds =
			((long)native[3] << 40) | ((long)native[2] << 32) | ((long)native[1] << 24) |
			((long)native[0] << 16) | ((long)native[5] << 8) | native[4];

		return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime;
	}
}
