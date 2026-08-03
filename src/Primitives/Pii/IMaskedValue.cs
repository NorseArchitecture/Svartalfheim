namespace Norse.Primitives.Pii;

/// <summary>
/// The masking law every PII type carries, and the marker the retention analyzer keys on:
/// implementing this interface is what makes a type PII in the compiler's eyes (NORSE061/NORSE062).
/// A type cannot opt into PII governance while opting out of masking — they are the same symbol.
/// </summary>
public interface IMaskedValue
{
	/// <summary>
	/// The pure, clock-free masked rendering — what <see cref="object.ToString"/> and the JSON write
	/// path emit. A value, never prose: no labels, no English inside the string.
	/// </summary>
	string Masked { get; }

	/// <summary>
	/// The disclosure-time masked rendering as of <paramref name="asOf"/>. Most implementers ignore
	/// the parameter and return <see cref="Masked"/>; time-dependent masks (current age) are a pure
	/// function of (value, asOf) — no clock lives in a primitive.
	/// </summary>
	string ToMasked(DateOnly asOf);
}
