namespace Norse.Primitives.Pii;

/// <summary>
/// Declares the retention basis for a persisted PII property. Property/field targets only — the
/// classification law is per field, never per table; there is no entity-level shorthand. Required by
/// NORSE061 on every persisted property whose type implements <see cref="IMaskedValue"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class RetentionPolicyAttribute : Attribute
{
	/// <summary>Declares the retention basis, with an optional statutory citation.</summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="basis"/> is the sentinel.</exception>
	public RetentionPolicyAttribute(RetentionBasis basis, string? citation = null)
	{
		if (basis is RetentionBasis.Unspecified)
			throw new ArgumentOutOfRangeException(nameof(basis), basis, "A retention declaration always names its basis.");
		Basis = basis;
		Citation = citation;
	}

	/// <summary>The declared basis.</summary>
	public RetentionBasis Basis { get; }

	/// <summary>The statutory citation, when the basis demands one.</summary>
	public string? Citation { get; }
}
