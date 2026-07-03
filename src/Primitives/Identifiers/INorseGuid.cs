namespace Norse.Primitives.Identifiers;

/// <summary>
/// Marker shared by every well-formed, version/variant-guaranteed Norse identifier type.
/// </summary>
public interface INorseGuid
{
	/// <summary>Gets the underlying <see cref="Guid"/> value.</summary>
	Guid Value { get; }
}
