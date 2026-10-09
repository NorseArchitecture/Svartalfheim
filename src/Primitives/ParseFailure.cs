namespace Norse.Primitives;

/// <summary>
/// The closed set of reasons a scalar→domain conversion can fail. The first four members mirror
/// HyperCast's <c>CastFailure</c> by name, number, and meaning — the engine's verdict reinterprets
/// into the forge's vocabulary without a switch; <see cref="Duplicate"/> is the forge's own.
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
	Malformed = 2,

	/// <summary>
	/// Input was well-formed but the value falls outside the target's representable range —
	/// <c>"256"</c> for a <see cref="byte"/>, a timestamp past 9999, a code point past
	/// <see cref="char.MaxValue"/>. Adopted from HyperCast's <c>CastFailure.OutOfRange</c> verbatim.
	/// </summary>
	OutOfRange = 3,

	/// <summary>
	/// Input token was individually valid but repeated where each token may appear only once
	/// — first consumer: flags-enum array parsing, a governed name appearing twice. The forge's
	/// own member, past the four it shares with HyperCast.
	/// </summary>
	Duplicate = 4
}
