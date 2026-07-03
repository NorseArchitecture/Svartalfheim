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
}
