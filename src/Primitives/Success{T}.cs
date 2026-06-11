namespace Norse.Primitives;

/// <summary>The success case of <see cref="Result{T}"/>: a validated domain value.</summary>
/// <typeparam name="T">The validated value's type. Non-nullable by construction.</typeparam>
/// <param name="Value">The validated value.</param>
public readonly record struct Success<T>(T Value) where T : notnull;
