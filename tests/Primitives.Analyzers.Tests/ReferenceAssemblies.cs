using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Norse.Primitives.Analyzers.Tests;

static class ReferenceAssemblies
{
	public static readonly MetadataReference[] Bcl =
	[
		MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
		MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
		MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
		MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(System.Runtime.Serialization.DataContractAttribute).Assembly.Location),
		// [ServiceContract]/[OperationContract] ship via the System.ServiceModel.Primitives NuGet
		// package, not core BCL — confirmed against Heimdall's real AuthN.Services.csproj, which
		// references the same package explicitly rather than assuming it's ambient.
		MetadataReference.CreateFromFile(typeof(System.ServiceModel.ServiceContractAttribute).Assembly.Location)
	];
}
