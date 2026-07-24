namespace Norse.Primitives;

/// <summary>
/// The closed set of reasons a scalar→domain conversion can fail.
/// Adding a member is a deliberate breaking change: every exhaustive switch
/// over this enum becomes a build error until updated.
/// </summary>
public enum ParseFailure : byte
{
	/// <summary>Sentinel CLR default — never produced by any parse path.</summary>
	Unspecified = 0,

	/// <summary>Required input was empty or whitespace.</summary>
	Empty = 1,

	/// <summary>Input was present but not recognizable as the target type.</summary>
	Malformed = 2
}
