namespace Norse.Primitives;

/// <summary>
/// The closed set of reasons a scalar→domain conversion can fail. Mirrors HyperCast's
/// <c>CastFailure</c> for the four shared cases (<see cref="Unspecified"/>-<see cref="OutOfRange"/>)
/// by name, number, and semantics, plus this realm's own <see cref="Duplicate"/> — HyperCast is the
/// source of truth for the parsing grammar and its failure vocabulary; this realm's own addition
/// stays additive, never conflicting. Adding a member is a deliberate breaking change: every
/// exhaustive switch over this enum becomes a build error until updated.
/// </summary>
public enum ParseFailure : byte
{
	/// <summary>Sentinel CLR default — never produced by any parse path.</summary>
	Unspecified = 0,

	/// <summary>Required input was empty or whitespace.</summary>
	Empty = 1,

	/// <summary>Input was present but not recognizable as the target type.</summary>
	Malformed = 2,

	/// <summary>
	/// Input was well-formed but the value falls outside the target's representable range —
	/// e.g. <c>256</c> for a <see cref="byte"/>, a timestamp past <c>9999-12-31</c>.
	/// </summary>
	OutOfRange = 3,

	/// <summary>
	/// Input token was individually valid but repeated where each token may appear only once
	/// — first consumer: flags-enum array parsing, a governed name appearing twice.
	/// </summary>
	Duplicate = 4
}
