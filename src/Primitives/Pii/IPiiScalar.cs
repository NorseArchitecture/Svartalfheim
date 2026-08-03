namespace Norse.Primitives.Pii;

/// <summary>
/// The generic contract a PII scalar struct fulfills so infrastructure (the encrypting EF value
/// converter, the disclosure surface) can round-trip it without knowing the concrete type.
/// <see cref="WireValue"/> is the named, deliberate plaintext egress — the canonical wire string the
/// transport contracts carry; every accidental rendering path goes through
/// <see cref="IMaskedValue.Masked"/> instead.
/// </summary>
public interface IPiiScalar<TSelf> : IMaskedValue where TSelf : struct, IPiiScalar<TSelf>
{
	/// <summary>The canonical unmasked wire string. Deliberate egress only.</summary>
	string WireValue { get; }

	/// <summary>Parses the canonical wire form. Untrusted input — no throwing path.</summary>
	static abstract Result<TSelf> Parse(ReadOnlySpan<char> value);
}
