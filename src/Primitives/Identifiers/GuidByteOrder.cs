namespace Norse.Primitives.Identifiers;

/// <summary>
/// Identifies which byte layout a SequentialGuid's <see cref="Guid"/> value is currently in.
/// </summary>
/// <remarks>
/// Not detectable from the bits alone by design — the RFC 9562 version nibble and variant bits sit at
/// identical native byte offsets in both layouts, so an instance always carries its own tag rather than
/// relying on a runtime heuristic to guess.
/// </remarks>
public enum GuidByteOrder
{
	/// <summary>Sentinel CLR default — never a valid argument; guards against <c>default(GuidByteOrder)</c>.</summary>
	Unspecified = 0,

	/// <summary>
	/// RFC 9562 byte order — the layout <see cref="Guid(ReadOnlySpan{byte}, bool)"/> with <c>bigEndian: true</c>
	/// produces, and the layout every newly generated SequentialGuid starts in.
	/// </summary>
	Rfc9562 = 1,

	/// <summary>
	/// Byte order that sorts correctly under <see cref="System.Data.SqlTypes.SqlGuid"/> comparison
	/// (SQL Server's <c>uniqueidentifier</c> ordering).
	/// </summary>
	SqlServer = 2
}
