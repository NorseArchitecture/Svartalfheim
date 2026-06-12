using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Norse.Primitives.Benchmarks;

// BDN 0.15.x does not recognize the net11.0 preview runtime moniker; the default
// out-of-process toolchain crashes in SDK validation. In-process emit sidesteps the
// moniker entirely. Revisit when BDN learns .NET 11.
var config = DefaultConfig.Instance
	.AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance).AsDefault());
BenchmarkSwitcher.FromAssembly(typeof(StorageBenchmarks).Assembly).Run(args, config);
