using Microsoft.CodeAnalysis;

namespace Norse.Primitives.Analyzers;

/// <summary>
/// BFS over the composition closure of a property type — public instance properties, collection and
/// array element types, cycle-safe — answering one question: is an <c>IMaskedValue</c> implementer
/// reachable anywhere inside? Mirrors <see cref="ResponseClosureWalker"/>; skips <c>string</c>,
/// primitives, and framework special types.
/// </summary>
static class PiiCompositionWalker
{
	public static INamedTypeSymbol? FindReachablePii(ITypeSymbol root, INamedTypeSymbol maskedValue)
	{
		var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
		Queue<ITypeSymbol> queue = new();
		queue.Enqueue(root);
		while (queue.Count > 0)
		{
			var current = Unwrap(queue.Dequeue());
			if (!visited.Add(current) || current.SpecialType != SpecialType.None)
				continue;
			if (Implements(current, maskedValue))
				return current as INamedTypeSymbol;
			foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
			{
				if (property is { IsStatic: false, DeclaredAccessibility: Accessibility.Public })
					queue.Enqueue(property.Type);
			}
		}
		return null;
	}

	public static bool Implements(ITypeSymbol type, INamedTypeSymbol maskedValue) =>
		type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, maskedValue));

	public static ITypeSymbol Unwrap(ITypeSymbol type)
	{
		var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
		return Unwrap(type, visited);
	}

	// Cycle-safe: a self-referential enumerable (class Node : IEnumerable<Node>) or a mutual pair
	// (Foo : IEnumerable<Bar>, Bar : IEnumerable<Foo>) would otherwise recurse without bound and
	// StackOverflowException the process. Once a type is revisited, it is returned unchanged — the
	// caller sees "unwrapping made no progress" rather than looping forever.
	static ITypeSymbol Unwrap(ITypeSymbol type, HashSet<ITypeSymbol> visited)
	{
		if (!visited.Add(type))
			return type;
		if (type is IArrayTypeSymbol array)
			return Unwrap(array.ElementType, visited);
		if (type is INamedTypeSymbol named)
		{
			if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
				return Unwrap(named.TypeArguments[0], visited);
			var enumerable = named.AllInterfaces
				.FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
			if (enumerable is not null && named.SpecialType != SpecialType.System_String)
				return Unwrap(enumerable.TypeArguments[0], visited);
		}
		return type;
	}
}
