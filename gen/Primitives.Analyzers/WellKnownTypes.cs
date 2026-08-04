using Microsoft.CodeAnalysis;

namespace Norse.Primitives.Analyzers;

/// <summary>
/// Every symbol NORSE060 keys on, resolved once per compilation via
/// <see cref="Compilation.GetTypeByMetadataName"/> — matched by fully-qualified metadata name only,
/// mirroring Midgard's <c>ContractDiscovery</c>/<c>ClosureWalker</c> technique for types this project
/// never references directly (<c>Outcome&lt;T&gt;</c> lives in Asgard; Svartálfheim rides beneath Asgard,
/// never the reverse). <c>System.ServiceModel.*</c> ships via the System.ServiceModel.Primitives NuGet
/// package, not core BCL (Heimdall's <c>AuthN.Services.csproj</c> references it explicitly) — this
/// project still needs no reference to it, since <c>GetTypeByMetadataName</c> resolves the string
/// against whatever the CONSUMING compilation references, not this analyzer assembly's own references.
/// </summary>
readonly struct WellKnownTypes
{
	// Plain constructor-initialized struct, not a positional record — netstandard2.0 has no
	// System.Runtime.CompilerServices.IsExternalInit, and this project deliberately carries no
	// reference (not even Asgard's Abstractions.Emit polyfill Midgard's gen/ projects pull in) per the
	// design's "no project/package reference beyond what ships in the base class libraries" constraint.
	WellKnownTypes(
		INamedTypeSymbol serviceContractAttribute,
		INamedTypeSymbol operationContractAttribute,
		INamedTypeSymbol resultType,
		INamedTypeSymbol? taskOpen,
		INamedTypeSymbol? valueTaskOpen,
		INamedTypeSymbol? outcomeType,
		INamedTypeSymbol? enumerableOpen)
	{
		ServiceContractAttribute = serviceContractAttribute;
		OperationContractAttribute = operationContractAttribute;
		ResultType = resultType;
		TaskOpen = taskOpen;
		ValueTaskOpen = valueTaskOpen;
		OutcomeType = outcomeType;
		EnumerableOpen = enumerableOpen;
	}

	public INamedTypeSymbol ServiceContractAttribute { get; }
	public INamedTypeSymbol OperationContractAttribute { get; }
	public INamedTypeSymbol ResultType { get; }
	public INamedTypeSymbol? TaskOpen { get; }
	public INamedTypeSymbol? ValueTaskOpen { get; }
	public INamedTypeSymbol? OutcomeType { get; }
	public INamedTypeSymbol? EnumerableOpen { get; }

	/// <summary>Null when any of the two law-defining symbols (<c>[ServiceContract]</c>, <c>Result&lt;T&gt;</c>) is unresolvable — the analyzer has nothing to police in that compilation.</summary>
	public static WellKnownTypes? Resolve(Compilation compilation)
	{
		var serviceContract = compilation.GetTypeByMetadataName("System.ServiceModel.ServiceContractAttribute");
		var operationContract = compilation.GetTypeByMetadataName("System.ServiceModel.OperationContractAttribute");
		var resultType = compilation.GetTypeByMetadataName("Norse.Primitives.Result`1");
		if (serviceContract is null || operationContract is null || resultType is null)
			return null;

		return new WellKnownTypes(
			serviceContract,
			operationContract,
			resultType,
			compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
			compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"),
			compilation.GetTypeByMetadataName("Norse.Abstractions.Contracts.Outcome`1"),
			compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1"));
	}
}

/// <summary>
/// The NORSE061/NORSE062 symbol set, resolved independently of <see cref="WellKnownTypes"/> — a
/// compilation missing <c>[ServiceContract]</c>/<c>Result&lt;T&gt;</c> still runs the retention
/// analyzer, and vice versa. <c>IMaskedValue</c>/<c>RetentionPolicyAttribute</c> are same-package
/// symbols (they ship inside this project's own host package, Norse.Primitives) and always resolve in
/// practice; <c>INorseEntity&lt;TSelf&gt;</c> is the one symbol that can genuinely be absent — no
/// Norse.Persistence.EntityFramework reference means no persisted roots can exist, so the whole
/// analyzer correctly self-disables rather than reporting on nothing.
/// </summary>
readonly struct RetentionWellKnownTypes
{
	RetentionWellKnownTypes(INamedTypeSymbol maskedValue, INamedTypeSymbol retentionPolicyAttribute, INamedTypeSymbol norseEntity)
	{
		MaskedValue = maskedValue;
		RetentionPolicyAttribute = retentionPolicyAttribute;
		NorseEntity = norseEntity;
	}

	public INamedTypeSymbol MaskedValue { get; }
	public INamedTypeSymbol RetentionPolicyAttribute { get; }
	public INamedTypeSymbol NorseEntity { get; }

	/// <summary>Null when <c>IMaskedValue</c>, <c>RetentionPolicyAttribute</c>, or <c>INorseEntity&lt;TSelf&gt;</c> is unresolvable in the consuming compilation.</summary>
	public static RetentionWellKnownTypes? Resolve(Compilation compilation)
	{
		var maskedValue = compilation.GetTypeByMetadataName("Norse.Primitives.Pii.IMaskedValue");
		var retentionPolicyAttribute = compilation.GetTypeByMetadataName("Norse.Primitives.Pii.RetentionPolicyAttribute");
		var norseEntity = compilation.GetTypeByMetadataName("Norse.Persistence.EntityFramework.INorseEntity`1");
		if (maskedValue is null || retentionPolicyAttribute is null || norseEntity is null)
			return null;

		return new RetentionWellKnownTypes(maskedValue, retentionPolicyAttribute, norseEntity);
	}
}
