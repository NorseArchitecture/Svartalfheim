using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Norse.Primitives.Analyzers;

/// <summary>
/// Resolves a <c>[ServiceContract]</c>/<c>[OperationContract]</c> method's response payload type
/// (unwrapping <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c> then <c>Outcome&lt;T&gt;</c>) and walks
/// every type reachable from it through public instance properties — including into collection item
/// types — reporting NORSE060 on any property whose type is <c>Result&lt;T&gt;</c> or
/// <c>Result&lt;T&gt;?</c>. Defensive against cyclic type graphs via a visited set, even though the
/// platform's own shape law elsewhere forbids cycles — this walker is more general-purpose and must not
/// assume that law holds everywhere it might run. Never walks method PARAMETER types — the caller only
/// ever hands this the resolved response payload.
/// </summary>
static class ResponseClosureWalker
{
	static readonly SymbolDisplayFormat _displayFormat = SymbolDisplayFormat.FullyQualifiedFormat;

	public static void AnalyzeOperation(SymbolAnalysisContext context, INamedTypeSymbol serviceInterface, IMethodSymbol operation, WellKnownTypes wellKnown)
	{
		if (ResolvePayload(operation.ReturnType, wellKnown) is not INamedTypeSymbol payload)
			return;

		var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
		Queue<INamedTypeSymbol> queue = [];
		if (visited.Add(payload))
			queue.Enqueue(payload);

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			foreach (var property in GetInstanceProperties(current))
				ProcessProperty(context, property, current, serviceInterface, operation, wellKnown, visited, queue);
		}
	}

	/// <summary>
	/// One property, one verdict: report NORSE060 if the property's own type — or, one layer deeper, a
	/// collection property's ITEM type — is <c>Result&lt;T&gt;</c>/<c>Result&lt;T&gt;?</c>; otherwise
	/// enqueue whatever complex type is reachable from it (the property itself, or its collection item
	/// type) for further walking. A collection whose item type IS <c>Result&lt;T&gt;</c> (<c>List&lt;Result&lt;T&gt;&gt;</c>,
	/// <c>Result&lt;T&gt;[]</c>, any <c>IEnumerable&lt;Result&lt;T&gt;&gt;</c>) is exactly the same law
	/// violation as a direct <c>Result&lt;T&gt;</c> property, one collection layer removed — reported on
	/// the collection property itself, never silently dropped.
	/// </summary>
	static void ProcessProperty(SymbolAnalysisContext context, IPropertySymbol property, INamedTypeSymbol declaringType, INamedTypeSymbol serviceInterface, IMethodSymbol operation, WellKnownTypes wellKnown, HashSet<INamedTypeSymbol> visited, Queue<INamedTypeSymbol> queue)
	{
		var propertyType = UnwrapNullable(property.Type);

		if (IsResultType(propertyType, wellKnown.ResultType))
		{
			Report(context, property, declaringType, serviceInterface, operation);
			return;
		}

		if (IsEnumerableItem(propertyType, wellKnown.EnumerableOpen, out var itemType))
		{
			var unwrappedItem = UnwrapNullable(itemType);
			if (IsResultType(unwrappedItem, wellKnown.ResultType))
			{
				Report(context, property, declaringType, serviceInterface, operation);
				return;
			}

			EnqueueComplex(unwrappedItem, wellKnown, visited, queue);
			return;
		}

		EnqueueComplex(propertyType, wellKnown, visited, queue);
	}

	/// <summary>Unwraps zero-or-one <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c> layer, then requires exactly <c>Outcome&lt;T&gt;</c> — the platform's one confirmed wire-method shape (verified against real production <c>[ServiceContract]</c> interfaces, e.g. Heimdall's <c>IAuthenticationService</c>, Mímir's <c>IReferenceService</c>). Any other shape yields no payload — nothing for this analyzer to police.</summary>
	static ITypeSymbol? ResolvePayload(ITypeSymbol returnType, WellKnownTypes wellKnown)
	{
		if (wellKnown.OutcomeType is null)
			return null;

		var current = returnType;
		if (current is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } asyncWrapper &&
			(SymbolEqualityComparer.Default.Equals(asyncWrapper.OriginalDefinition, wellKnown.TaskOpen) ||
			 SymbolEqualityComparer.Default.Equals(asyncWrapper.OriginalDefinition, wellKnown.ValueTaskOpen)))
		{
			current = asyncWrapper.TypeArguments[0];
		}

		if (current is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } outcome &&
			SymbolEqualityComparer.Default.Equals(outcome.OriginalDefinition, wellKnown.OutcomeType))
		{
			return outcome.TypeArguments[0];
		}

		return null;
	}

	/// <summary>
	/// Adds a class/struct type to the walk queue unless it's a terminal scalar or already visited (the
	/// cycle guard). By the time a type reaches this method it has already cleared <see cref="ProcessProperty"/>'s
	/// own <c>Result&lt;T&gt;</c> check (direct or one collection layer deep) and been diagnosed there
	/// instead of enqueued — the <c>!IsResultType</c> guard below is a defensive backstop against a future
	/// caller reaching this method without that check, never the primary place <c>Result&lt;T&gt;</c> gets
	/// caught. <c>Result&lt;T&gt;</c> is never silently dropped on this path: either it's diagnosed before
	/// this call, or (if it somehow arrives here anyway) it's excluded from the queue rather than walked
	/// into for irrelevant internal members — it is never simply ignored.
	/// </summary>
	static void EnqueueComplex(ITypeSymbol type, WellKnownTypes wellKnown, HashSet<INamedTypeSymbol> visited, Queue<INamedTypeSymbol> queue)
	{
		if (type is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } complex &&
			!IsTerminalScalar(complex) &&
			!IsResultType(complex, wellKnown.ResultType) &&
			visited.Add(complex))
		{
			queue.Enqueue(complex);
		}
	}

	static void Report(SymbolAnalysisContext context, IPropertySymbol property, INamedTypeSymbol declaringType, INamedTypeSymbol serviceInterface, IMethodSymbol operation)
	{
		var location = property.Locations.Length > 0 ? property.Locations[0] : Location.None;
		context.ReportDiagnostic(Diagnostic.Create(
			Diagnostics.ResultInServiceResponse,
			location,
			property.Name,
			declaringType.ToDisplayString(_displayFormat),
			serviceInterface.ToDisplayString(_displayFormat),
			operation.Name));
	}

	static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
		type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : type;

	static bool IsResultType(ITypeSymbol type, INamedTypeSymbol resultType) =>
		type is INamedTypeSymbol { IsGenericType: true } named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, resultType);

	static bool IsEnumerableItem(ITypeSymbol type, INamedTypeSymbol? enumerableOpen, out ITypeSymbol itemType)
	{
		itemType = null!;
		if (enumerableOpen is null || type.SpecialType == SpecialType.System_String)
			return false;

		if (type is INamedTypeSymbol { IsGenericType: true } self && SymbolEqualityComparer.Default.Equals(self.OriginalDefinition, enumerableOpen))
		{
			itemType = self.TypeArguments[0];
			return true;
		}

		foreach (var candidate in type.AllInterfaces)
			if (candidate.IsGenericType && SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, enumerableOpen))
			{
				itemType = candidate.TypeArguments[0];
				return true;
			}

		return false;
	}

	static bool IsTerminalScalar(ITypeSymbol type)
	{
		if (type.TypeKind == TypeKind.Enum)
			return true;

		return type.SpecialType is
			SpecialType.System_Boolean or SpecialType.System_SByte or SpecialType.System_Byte or
			SpecialType.System_Int16 or SpecialType.System_UInt16 or
			SpecialType.System_Int32 or SpecialType.System_UInt32 or
			SpecialType.System_Int64 or SpecialType.System_UInt64 or
			SpecialType.System_Decimal or SpecialType.System_Single or SpecialType.System_Double or
			SpecialType.System_Char or SpecialType.System_String or SpecialType.System_Object
			|| IsKnownScalarStruct(type);
	}

	static bool IsKnownScalarStruct(ITypeSymbol type) =>
		type is INamedTypeSymbol { ContainingNamespace.Name: "System" } named &&
		named.Name is "Guid" or "DateTime" or "DateTimeOffset" or "DateOnly" or "TimeOnly" or "TimeSpan";

	static IEnumerable<IPropertySymbol> GetInstanceProperties(INamedTypeSymbol type) =>
		type.GetMembers().OfType<IPropertySymbol>().Where(p => p is { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public });
}
