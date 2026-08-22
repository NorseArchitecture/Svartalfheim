using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Norse.Architecture.Analyzers.Tests;

static class ReferenceAssemblies
{
	public static readonly MetadataReference[] Bcl =
	[
		MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Exception).Assembly.Location),
		MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
		MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
		MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(System.Runtime.Serialization.DataContractAttribute).Assembly.Location),
		// [ServiceContract]/[OperationContract] ship via the System.ServiceModel.Primitives NuGet
		// package, not core BCL — confirmed against Heimdall's real AuthN.Services.csproj, which
		// references the same package explicitly rather than assuming it's ambient.
		MetadataReference.CreateFromFile(typeof(System.ServiceModel.ServiceContractAttribute).Assembly.Location),
		// System.Text.Json needed for the JsonSerializer fixtures.
		MetadataReference.CreateFromFile(Assembly.Load("System.Text.Json").Location)
	];

	// Resolved via typeof(...).Assembly.Location against the FrameworkReference to Microsoft.AspNetCore.App
	// in this project's .csproj -- the same "ask the runtime, don't guess a shared-framework directory
	// layout" approach as Bcl above. Neither Microsoft.AspNetCore.Mvc.Core nor
	// Microsoft.AspNetCore.Builder/.Routing ship as standalone NuGet packages post-3.0 (only in the
	// shared framework), so a PackageReference is unavailable; FrameworkReference is the idiomatic way to
	// get compile-time types and a runtime-resolved assembly location without hand-walking directories.
	public static readonly MetadataReference[] AspNetCore =
	[
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute).Assembly.Location), // AllowAnonymousAttribute
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Authorization.IAllowAnonymous).Assembly.Location), // IAllowAnonymous
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Mvc.ControllerBase).Assembly.Location), // ControllerBase, Ok()
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Mvc.IActionResult).Assembly.Location), // IActionResult
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Builder.WebApplication).Assembly.Location), // WebApplication
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Builder.IEndpointConventionBuilder).Assembly.Location), // IEndpointConventionBuilder
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder).Assembly.Location), // IEndpointRouteBuilder, MapGet
		MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Builder.AuthorizationEndpointConventionBuilderExtensions).Assembly.Location), // AllowAnonymous()/RequireAuthorization() extensions
		MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Hosting.IHost).Assembly.Location) // WebApplication implements IHost
	];
}
