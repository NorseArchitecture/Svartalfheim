using System.Diagnostics;

namespace Norse.Architecture.Analyzers.Tests;

/// <summary>
///     The NORSE075 proof obligation, end to end: a real dotnet build of a real Razor project with
///     this analyzer attached. Slow by unit standards (seconds, plus a first-run restore) and worth
///     it — an SDK bump that changes the Razor generator's emitted shape fails here, loudly, instead
///     of silently blinding the rule in production.
/// </summary>
public sealed class SeamBoundFormBuildProofTests
{
	static async Task<(int ExitCode, string Output)> BuildFixture(string razorFile)
	{
		var fixtures = Path.Combine(AppContext.BaseDirectory, "BuildFixtures");
		// The analyzer project sits at a fixed offset from the test assembly inside the repo.
		var analyzerProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
			"../../../../../gen/Architecture.Analyzers/Architecture.Analyzers.csproj"));
		File.Exists(analyzerProject).ShouldBeTrue($"analyzer project not found at {analyzerProject}");

		var scratch = Directory.CreateTempSubdirectory("norse075-").FullName;
		// SDK hermeticity: the scratch dir is outside the repo, so it would otherwise build with the
		// machine's ambient SDK — a different Razor generator than the realm's. Copy the realm's own
		// global.json (three levels above the analyzer project) so the fixture builds on the exact
		// pinned baseline; a runtime copy can never drift from it.
		var globalJson = Path.GetFullPath(Path.Combine(analyzerProject, "../../../global.json"));
		File.Exists(globalJson).ShouldBeTrue($"realm global.json not found at {globalJson}");
		File.Copy(globalJson, Path.Combine(scratch, "global.json"));
		File.Copy(Path.Combine(fixtures, "StubForm.cs"), Path.Combine(scratch, "StubForm.cs"));
		File.Copy(Path.Combine(fixtures, razorFile), Path.Combine(scratch, razorFile));
		var csproj = await File.ReadAllTextAsync(Path.Combine(fixtures, "fixture.csproj.template"), TestContext.Current.CancellationToken);
		await File.WriteAllTextAsync(Path.Combine(scratch, "fixture.csproj"),
			csproj.Replace("$(NorseAnalyzerProject)", analyzerProject, StringComparison.Ordinal), TestContext.Current.CancellationToken);

		using Process process = new();
		process.StartInfo = new()
		{
			FileName = "dotnet",
			Arguments = "build fixture.csproj -nologo -v:m",
			WorkingDirectory = scratch,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		process.Start();
		var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken)
			+ await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
		await process.WaitForExitAsync(TestContext.Current.CancellationToken);
		Directory.Delete(scratch, recursive: true);
		return (process.ExitCode, output);
	}

	[Fact]
	async Task A_seam_bound_OnValidSubmit_form_fails_the_real_build_with_NORSE075()
	{
		var (exitCode, output) = await BuildFixture("ViolatingForm.razor");

		exitCode.ShouldNotBe(0, output);
		output.ShouldContain("NORSE075");
	}

	[Fact]
	async Task An_OnSubmit_form_builds_clean()
	{
		var (exitCode, output) = await BuildFixture("CleanForm.razor");

		exitCode.ShouldBe(0, output);
	}
}
