namespace Norse.Primitives.Identifiers;

/// <summary>
/// Checks the RFC 9562 version and variant bits on an already-constructed <see cref="Guid"/>, using
/// .NET's native (non-big-endian) byte layout — the same layout <see cref="Guid.TryWriteBytes(Span{byte})"/>
/// produces without a byte-order argument.
/// </summary>
static class GuidVersionBits
{
	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="value"/> carries the RFC 9562 version nibble
	/// equal to <paramref name="version"/> and the RFC 9562 variant bits (top two bits <c>10</c>).
	/// </summary>
	internal static bool HasVersionAndVariant(Guid value, byte version)
	{
		Span<byte> native = stackalloc byte[16];
		value.TryWriteBytes(native);
		return (native[7] >> 4) == version && (native[8] & 0xC0) == 0x80;
	}
}
