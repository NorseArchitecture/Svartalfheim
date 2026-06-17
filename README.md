# Svartalfheim

> Home of the dvergar, the master smiths who forged Mjölnir, Gleipnir, and Gungnir.

The forge of the Norse Architecture — **`Norse.Primitives`**, the foundational realm. Everything crossing a trust boundary into the ecosystem from an untrusted source flows through the types forged here: the `Result<T>` discriminated union, its closed parse-failure vocabulary, and the hot-path scalar parsers.

## What's forged here

- **`Result<T>`** — a hand-authored C# custom native union over `Success<T>` / `Failure`: zero boxing on both paths, exhaustive two-arm switches, no way to construct an invalid value, and a law-proven combinator core (`Map` / `Bind` / `Match` — the functor and monad laws are FsCheck-pinned).
- **`ParseFailure`** — the closed conversion-failure vocabulary (`Empty`, `Malformed`); adding a member is a deliberate breaking change.
- **`Parser`** — the generic gateway over `ISpanParsable<T>`: span in, `Result<T>` out, uniform failure semantics, required format provider. Specialists ride JIT-eliminated `typeof` routes; there is no runtime registry — a type that cannot parse does not compile.
- **Hot-path parsers** — static specialists with `ParseRequired` / `ParseOptional` entry points over `ReadOnlySpan<char>` and honest signatures: `BooleanParser`, the generic-math numeric cores `IntegerParser` (`IBinaryInteger<T>`) and `RealParser` (`IFloatingPoint<T>` — `float`/`double`/`decimal`, finite values only), `CharParser`, and `GuidParser`. They carry the real ingestion vocabulary (grouping, currency, parentheses, hex/binary, percentage, code points, URN prefixes) the bare BCL `TryParse` lacks. Temporal parsers are the next increment. Ambiguous input fails loudly; nothing is guessed, nothing falls back silently.

Scalar → domain conversion only: application error categories and transport conditions belong to other realms by design.

## Build and test

```shell
dotnet build Svartalfheim.slnx   # warnings are errors — a single warning fails
dotnet test Svartalfheim.slnx    # xUnit v3 + Shouldly on Microsoft.Testing.Platform
```

Requires the .NET 11 preview SDK pinned by `global.json`. The realm builds standalone — it is its own clone target, not only a Bifrost submodule.

Evidence rigs: `benchmarks/Primitives.Benchmarks` (BenchmarkDotNet — storage, dispatch, and combinator cost, run manually in Release) and `tests/smoke/Primitives.Aot.Smoke` (the pathway must survive `PublishAot` with zero warnings and exit 0; needs the VS C++ build tools).

## The naming law

Project folders and `.csproj` files are brand-free (`src/Primitives/Primitives.csproj`); the realm's root `Directory.Build.props` injects `AssemblyName` and `RootNamespace` as `Norse.$(MSBuildProjectName)`. Fork it, change `Norse` once, and every build output carries your brand — the `namespace Norse.*` declarations in code are yours to cull deliberately, with no filesystem change either way.

## The cosmos

Svartalfheim is one realm of the [Norse Architecture](https://github.com/NorseArchitecture). The whole platform composes at [Bifrost](https://github.com/NorseArchitecture/Bifrost) — clone once, cross the bridge. Every design is tried in [Glitnir](https://github.com/NorseArchitecture/Glitnir), the design court, before code is forged here; this realm's specs and plans live in the court's `docs/superpowers/`.
