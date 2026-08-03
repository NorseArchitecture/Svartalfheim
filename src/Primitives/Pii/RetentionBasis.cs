namespace Norse.Primitives.Pii;

/// <summary>The declared legal basis under which a persisted PII field is retained.</summary>
public enum RetentionBasis : byte
{
	/// <summary>Sentinel CLR default — never a valid basis; a declaration always names its law.</summary>
	Unspecified = 0,
	/// <summary>Erased when the subject's key is destroyed (Class A/C — crypto-shredding).</summary>
	SubjectKey = 1,
	/// <summary>Retained under a statutory epoch key (Class B — reserved; cite the statute).</summary>
	StatutoryEpoch = 2
}
