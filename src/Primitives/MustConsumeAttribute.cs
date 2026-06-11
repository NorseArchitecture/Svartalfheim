namespace Norse.Primitives;

/// <summary>
/// Marks a type whose values must be consumed by the caller — pattern matched,
/// composed, returned, stored, or explicitly discarded. Enforced at build time
/// by the YGG201 analyzer (Norse.Primitives.Architecture, separate package).
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class MustConsumeAttribute : Attribute;
