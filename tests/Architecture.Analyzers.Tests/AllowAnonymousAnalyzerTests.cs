using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace Norse.Architecture.Analyzers.Tests;

public sealed class AllowAnonymousAnalyzerTests
{
	// Neither Microsoft.AspNetCore.Mvc.Core nor Microsoft.AspNetCore.Builder/.Routing ship as
	// standalone NuGet packages post-3.0 (only in the shared framework), so this project carries no
	// compile-time reference to them and typeof(...) is unavailable. Roslyn only needs the DLL bytes,
	// not a loadable reference, so the fixtures below resolve straight from the shared-framework
	// directory sitting beside the netcoreapp runtime this test host is already running on — the same
	// "resolved from the test's own runtime" spirit as ReferenceAssemblies.Bcl, one level further out.
	static readonly MetadataReference[] _aspNetReferences = ResolveAspNetCoreReferences();

	static MetadataReference[] ResolveAspNetCoreReferences()
	{
		var netCoreAppDir = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar);
		var sharedRoot = Directory.GetParent(Directory.GetParent(netCoreAppDir)!.FullName)!.FullName;
		var aspNetCoreRoot = Path.Combine(sharedRoot, "Microsoft.AspNetCore.App");
		var versionDir = Directory.GetDirectories(aspNetCoreRoot).OrderByDescending(d => d, StringComparer.Ordinal).First();

		string[] aspNetCoreAssemblies =
		[
			"Microsoft.AspNetCore.Authorization.dll", // AllowAnonymousAttribute
			"Microsoft.AspNetCore.Metadata.dll", // IAllowAnonymous
			"Microsoft.AspNetCore.Mvc.Core.dll", // ControllerBase, Ok()
			"Microsoft.AspNetCore.Mvc.Abstractions.dll", // IActionResult
			"Microsoft.AspNetCore.dll", // WebApplication
			"Microsoft.AspNetCore.Http.Abstractions.dll", // IEndpointConventionBuilder
			"Microsoft.AspNetCore.Routing.dll", // IEndpointRouteBuilder, MapGet
			"Microsoft.AspNetCore.Authorization.Policy.dll", // AllowAnonymous()/RequireAuthorization() extensions
		];
		var references = aspNetCoreAssemblies.Select(a => MetadataReference.CreateFromFile(Path.Combine(versionDir, a)));

		// WebApplication implements IHost, which ships in the netcoreapp shared framework rather than
		// the aspnetcore one -- needed for the fixture to bind WebApplication's base-type surface.
		var hostingAbstractions = MetadataReference.CreateFromFile(
			Path.Combine(netCoreAppDir, "Microsoft.Extensions.Hosting.Abstractions.dll"));

		return [.. references, hostingAbstractions];
	}

	// NORSE013 ships isEnabledByDefault: false (Task 4 brief) -- the Roslyn analyzer driver skips
	// Initialize entirely for an analyzer whose only diagnostic is disabled by default, unless the
	// compilation raises it explicitly. Opting in here is what lets these fixtures exercise the
	// analyzer at all; it says nothing about the diagnostic's default severity in a real build (still
	// off, confirmed by the ship-gate build in step 6).
	static readonly IReadOnlyDictionary<string, ReportDiagnostic> _enableNorse013 =
		new Dictionary<string, ReportDiagnostic> { ["NORSE013"] = ReportDiagnostic.Warn };

	static Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> Analyze(string source) =>
		AnalyzerTestHarness.GetDiagnosticsAsync(new AllowAnonymousAnalyzer(), "Norse.Fixture.Assembly", _enableNorse013, _aspNetReferences, source);

	[Fact]
	async Task Strikes_the_attribute_on_an_action()
	{
		const string Source = """
			using Microsoft.AspNetCore.Authorization;
			using Microsoft.AspNetCore.Mvc;

			public sealed class SampleController : ControllerBase
			{
				[AllowAnonymous]
				public IActionResult Get() => Ok();
			}
			""";

		var diagnostics = await Analyze(Source);
		diagnostics.ShouldContain(d => d.Id == "NORSE013");
	}

	[Fact]
	async Task Strikes_the_fluent_call_on_an_endpoint_builder()
	{
		const string Source = """
			using Microsoft.AspNetCore.Builder;

			public static class Wireup
			{
				public static void Map(WebApplication app) =>
					app.MapGet("/health", () => "ok").AllowAnonymous();
			}
			""";

		var diagnostics = await Analyze(Source);
		diagnostics.ShouldContain(d => d.Id == "NORSE013");
	}

	[Fact]
	async Task Allows_a_named_policy()
	{
		const string Source = """
			using Microsoft.AspNetCore.Builder;
			using Microsoft.AspNetCore.Authorization;

			public static class Wireup
			{
				public static void Map(WebApplication app) =>
					app.MapGet("/health", () => "ok").RequireAuthorization("Norse.Probe");
			}
			""";

		var diagnostics = await Analyze(Source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Strikes_a_custom_attribute_that_implements_the_marker()
	{
		const string Source = """
			using Microsoft.AspNetCore.Authorization;
			using Microsoft.AspNetCore.Mvc;

			public sealed class OpenAttribute : System.Attribute, IAllowAnonymous;

			public sealed class SampleController : ControllerBase
			{
				[Open]
				public IActionResult Get() => Ok();
			}
			""";

		var diagnostics = await Analyze(Source);
		diagnostics.ShouldContain(d => d.Id == "NORSE013");
	}

	[Fact]
	async Task Ignores_an_unrelated_user_method_that_happens_to_be_named_AllowAnonymous()
	{
		const string Source = """
			public sealed class Doorman
			{
				public Doorman AllowAnonymous() => this;
			}

			public static class Wireup
			{
				public static void Open() => new Doorman().AllowAnonymous();
			}
			""";

		var diagnostics = await Analyze(Source);
		diagnostics.ShouldBeEmpty();
	}

	[Fact]
	async Task Ignores_a_user_extension_named_AllowAnonymous_on_an_unrelated_receiver()
	{
		const string Source = """
			public static class StringExtensions
			{
				public static string AllowAnonymous(this string value) => value;
			}

			public static class Wireup
			{
				public static string Open() => "x".AllowAnonymous();
			}
			""";

		var diagnostics = await Analyze(Source);
		diagnostics.ShouldBeEmpty();
	}
}
