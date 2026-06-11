# CLAUDE.md — Svartalfheim (`Norse.Primitives`)

> **Do not commit, push, or rewrite git history.** Stage your edits (`git add`), show the diff, and stop — the human reviews in GitHub Desktop and commits. This applies even when a skill's flow includes a commit step.

> **Use US English spelling** in code, identifiers, comments, docs, and commit/PR copy.

Svartalfheim is the forge: `Norse.Primitives`, the foundational primitives realm of the Norse platform. Everything crossing a trust boundary into the ecosystem from untrusted sources flows through the types here — `Result<T>`, its case types, and the hot-path scalar parsers. Scalar→domain conversion only; application error categories (validation/not-found/conflict) belong to the mediator, transport conditions to the host pipeline.

**Authoritative documents (read in this order):**
1. `../Glitnir/docs/superpowers/specs/2026-06-11-svartalfheim-result-union-boolean-parser-design.md` — the first-increment spec, amended with implementation findings. The amendments ARE the record; trust them over the parent spec where they differ. (Its execution plan sits beside it under `plans/`.)
2. Parent spec: `../Glitnir/docs/superpowers/specs/2026-05-20-svartalfheim-primitives-design.md` (Glitnir is the design court — all specs, plans, and PoC verdicts live there; it sits beside this realm as a sibling submodule on the Bifrost bridge).
3. Prior-art lessons: the Crucible — the pre-union `Result<T>`, parser vocabulary, and test matrices this repo learned from. Private prior art; it lives outside this workspace by design and is cited by name, never by path.

---

## Build & Test

- `dotnet build Svartalfheim.slnx` — warnings are errors (WarningLevel 9999, EnforceCodeStyleInBuild). A single warning fails.
- `dotnet test Svartalfheim.slnx` — xUnit v3 + Shouldly on Microsoft.Testing.Platform. **VSTest `--filter` does NOT work**; filter with `dotnet test tests/Primitives.Tests -- --filter-class "*.ResultTests"`.
- SDK pinned by `global.json`: `11.0.100-` prerelease, rollForward latestFeature. C# `LangVersion=preview`.
- NEVER `dotnet test` a test project that contains zero tests — xUnit v3 fails the run.

## Architecture Facts (decided — do not re-litigate)

- **`Result<T>` is a hand-authored C# 15 custom union**, not a shorthand `union` declaration: shorthand declarations store `object?` and box every value-type case. The custom pattern (`[Union]` readonly record struct + per-case public ctors + `Value` + `HasValue`/`TryGetValue`) keeps both cases inline — zero boxing on both paths (IL-verified). Consumers see full native union semantics.
- **`result is Result<T>` is a compile error (CS8121)** — union patterns unwrap to contents. Match `Success<T>` / `Failure`; a two-arm switch is exhaustive without a discard.
- **`default(Result<T>)` / `default(Failure)` are malformed by construction** (the `ImmutableArray<T>` footgun class): documented in XML remarks, pinned by canary tests, future YGG analyzer rule planned. `Result(Failure)` throws on a smuggled `Unspecified` sentinel.
- **Truncation knowledge lives in `Failure` alone**: parsers pass their trimmed `ReadOnlySpan<char>` to the span ctor overload, which bounds to `MaxInputLength` (256) before allocating. Never pre-truncate in a parser.
- **Parser template** (BooleanParser is the precedent for ~20 more): static class, `ParseRequired(ReadOnlySpan<char>) → Result<T>` + `ParseOptional(ReadOnlySpan<char>) → Result<T>?`, shared private `Parse`, honest signatures (no `IFormatProvider` on culture-insensitive parsers — a parameter documented as ignored is a lie). Empty→`ParseFailure.Empty`/absent; unrecognized→`Malformed`. No implicit `string → Result<T>` conversions, ever — parsing is an explicit named call.
- **`ParseFailure` is closed** (Unspecified sentinel / Empty / Malformed). Adding a member is a deliberate breaking change.

## Toolchain Gotchas (.NET 11 preview 5)

- The runtime SHIPS `UnionAttribute`/`IUnion` — the ref pack's XML doc file doesn't list them (false negative). Probe ref assemblies, not XML docs. Never re-add local polyfills (CS0436).
- xUnit v3 on MTP requires `<OutputType>Exe</OutputType>` on test projects (lives in `tests/Directory.Build.props`).
- IDE0005 in build mode requires `GenerateDocumentationFile=true` — hence tests keep doc generation on and suppress CS1591 instead. Don't invert that trade.
- **ReSharper/Rider do not understand C# 15 unions yet** (even EAP builds) — union-related squiggles are visual noise. The compiler is the truth; never alter working union code to appease R#.

## House Style

- **Project names are brand-free** (`src/Primitives/Primitives.csproj`); `Directory.Build.props` injects `AssemblyName`/`RootNamespace` as `Norse.$(MSBuildProjectName)`. The brand prefix lives in that one file so a fork rebrands without renaming a single project — build derivatives (`InternalsVisibleTo` via `$(AssemblyName)`) follow automatically; `namespace Norse.*` declarations in code stay until the fork deliberately culls them.

Tabs (except YAML/MD per .editorconfig) · `var` for returns, explicit type + `new()` for construction · omit default accessibility modifiers · XML docs mandatory on all public src members (CS1591 is an error in src) · test naming `Should_{behavior}_when_{condition}`, test classes `public sealed`, test methods omit access modifiers · Shouldly/Xunit usings are global (injected via tests props — never add them per-file).

## Deferred Increments (spec §10 — in rough order)

1. Generic `Parser.ParseRequired<T>`/`ParseOptional<T>` gateway over `ISpanParsable<T>` (routes `bool` to BooleanParser via JIT-eliminated `typeof(T)` branch — no runtime registry).
2. Combinators (`Map`/`Bind`/`Match`/`Combine`/`*Present`) + FsCheck monad-law properties — ship together.
3. `Norse.Primitives.Aot.SmokeTests` (PublishAot gate).
4. `Norse.Primitives.Benchmarks` (BenchmarkDotNet, zero-alloc verification).
5. Remaining hot-path parsers, NuGet packaging metadata.

Each increment is spec-first: brainstorm → spec → plan → code, with explicit human greenlights at each transition.
