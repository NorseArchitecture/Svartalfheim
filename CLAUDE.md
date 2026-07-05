# CLAUDE.md — Svartalfheim (`Norse.Primitives`)

## 0. Wrong Root — Halt

If you are reading this because **Svartalfheim itself is the Claude Code session root** — someone ran `claude` from inside this directory instead of `../Bifrost` — stop here. Do not read further, do not propose changes, do not run anything.

Tell the user: every Norse Architecture session starts from **Bifrost**. Org-wide settings (the `superpowers` plugin, permission rules) only apply when Bifrost is the actual session root — Claude Code never merges a submodule's own `.claude/settings.json` into a parent-launched session. Exit, `cd ../Bifrost`, and run `claude` there instead.

This repo's own `.claude/settings.json` carries a `SessionStart` hook that should already have blocked this session before this file was ever read. If you're reading this anyway, hooks were bypassed, disabled, or failed — halt regardless; this rule does not depend on the hook to hold.

---

> **Do not commit, push, or rewrite git history.** Stage your edits (`git add`), show the diff, and stop — the human reviews in GitHub Desktop and commits. This applies even when a skill's flow includes a commit step.

> **Use US English spelling** in code, identifiers, comments, docs, and commit/PR copy.

Svartalfheim is the forge: `Norse.Primitives`, the foundational primitives realm of the Norse platform. Everything crossing a trust boundary into the ecosystem from untrusted sources flows through the types here — `Result<T>`, its case types, and the hot-path scalar parsers. Scalar→domain conversion only; application error categories (validation/not-found/conflict) belong to the mediator, transport conditions to the host pipeline.

**Authoritative documents (read in this order):**
1. `../Glitnir/docs/Svartalfheim/specs/2026-06-17-svartalfheim-numeric-char-guid-parsers-design.md` — the third-increment spec (numeric family, `char`, `Guid` — two generic-math cores `IntegerParser`/`RealParser` plus `CharParser`/`GuidParser`, all routed through the gateway). Its execution plan sits beside it under `plans/`; §9 carries the temporal-parser ledger forward (the next increment).
2. `../Glitnir/docs/Svartalfheim/specs/2026-06-11-svartalfheim-pathway-proof-design.md` — the second-increment spec (gateway, combinators, evidence rigs), amended with benchmark findings (§8). (Its execution plan sits beside it under `plans/`.)
3. `../Glitnir/docs/Svartalfheim/specs/2026-06-11-svartalfheim-result-union-boolean-parser-design.md` — the first-increment spec, amended with implementation findings. The amendments ARE the record; trust them over the parent spec where they differ. (Its execution plan sits beside it under `plans/`.)
4. Parent spec: `../Glitnir/docs/Svartalfheim/specs/2026-05-20-svartalfheim-primitives-design.md` (Glitnir is the design court — all specs, plans, and PoC verdicts live there; it sits beside this realm as a sibling submodule on the Bifrost bridge).
5. Prior-art lessons: the Crucible — the pre-union `Result<T>`, parser vocabulary, and test matrices this repo learned from. Private prior art; it lives outside this workspace by design and is cited by name, never by path.

---

## Build & Test

- `dotnet build Svartalfheim.slnx` — warnings are errors (WarningLevel 9999, EnforceCodeStyleInBuild). A single warning fails.
- `dotnet test Svartalfheim.slnx` — xUnit v3 + Shouldly on Microsoft.Testing.Platform. **VSTest `--filter` does NOT work**; filter with `dotnet test tests/Primitives.Tests -- --filter-class "*.ResultTests"`.
- SDK pinned by `global.json`: `11.0.100-` prerelease, rollForward latestFeature. C# `LangVersion=preview`.
- NEVER `dotnet test` a test project that contains zero tests — xUnit v3 fails the run.
- Benchmarks (manual, Release): `dotnet run -c Release --project benchmarks/Primitives.Benchmarks -- --filter *`. Findings are court filings — file them as amendments to the pathway spec in Glitnir, never as loose notes.
- AOT smoke: `dotnet publish tests/smoke/Primitives.Aot.Smoke -c Release`, then run the native exe — zero AOT warnings and exit 0 required. Needs the VS "Desktop development with C++" workload on Windows.

## Architecture Facts (decided — do not re-litigate)

- **`Result<T>` is a hand-authored C# 15 custom union**, not a shorthand `union` declaration: shorthand declarations store `object?` and box every value-type case. The custom pattern (`[Union]` readonly record struct + per-case public ctors + `Value` + `HasValue`/`TryGetValue`) keeps both cases inline — zero boxing on both paths (IL-verified). Consumers see full native union semantics.
- **`result is Result<T>` is a compile error (CS8121)** — union patterns unwrap to contents. Match `Success<T>` / `Failure`; a two-arm switch is exhaustive without a discard.
- **`default(Result<T>)` / `default(Failure)` are malformed by construction** (the `ImmutableArray<T>` footgun class): documented in XML remarks, pinned by canary tests, future YGG analyzer rule planned. `Result(Failure)` throws on a smuggled `Unspecified` sentinel.
- **Truncation knowledge lives in `Failure` alone**: parsers pass their trimmed `ReadOnlySpan<char>` to the span ctor overload, which bounds to `MaxInputLength` (256) before allocating. Never pre-truncate in a parser.
- **Parser template** (BooleanParser is the precedent for ~20 more): static class, `ParseRequired(ReadOnlySpan<char>) → Result<T>` + `ParseOptional(ReadOnlySpan<char>) → Result<T>?`, shared private `Parse`, honest signatures (no `IFormatProvider` on culture-insensitive parsers — a parameter documented as ignored is a lie). Empty→`ParseFailure.Empty`/absent; unrecognized→`Malformed`. No implicit `string → Result<T>` conversions, ever — parsing is an explicit named call.
- **`ParseFailure` is closed** (Unspecified sentinel / Empty / Malformed). Adding a member is a deliberate breaking change.
- **`Parser` is the generic gateway** over `where T : notnull, ISpanParsable<T>` — `bool` routes to `BooleanParser` via a JIT-eliminated `typeof` branch (`Unsafe.As` identity reinterpret; sound because `T` is statically `bool` inside the branch — benchmark-verified at ratio 1.02); everything else goes through `T.TryParse(span, provider)`. The provider is required and non-nullable — no defaulting overload, ever. No runtime registry: a type that cannot parse does not compile.
- **Combinators are `Map`/`Bind`/`Match` only** (law-proving core; `Combine`/async/`*Present` wait for a consumer). Instance methods implemented as union switches — a defaulted `Result<T>` throws `SwitchExpressionException` through them identically to a hand-written switch. The five functor/monad laws are FsCheck-pinned in `ResultLawTests` (portable `Prop.ForAll` style, no integration package). Measured tax: ~2.8× a hand-rolled switch, zero allocation — ergonomics, not the hot path.

## Toolchain Gotchas (.NET 11 preview 5)

- The runtime SHIPS `UnionAttribute`/`IUnion` — the ref pack's XML doc file doesn't list them (false negative). Probe ref assemblies, not XML docs. Never re-add local polyfills (CS0436).
- xUnit v3 on MTP requires `<OutputType>Exe</OutputType>` on test projects (lives in `tests/Directory.Build.props`).
- IDE0005 in build mode requires `GenerateDocumentationFile=true` — hence tests keep doc generation on and suppress CS1591 instead. Don't invert that trade.
- **ReSharper/Rider do not understand C# 15 unions yet** (even EAP builds) — union-related squiggles are visual noise. The compiler is the truth; never alter working union code to appease R#.
- **BenchmarkDotNet 0.15.x does not recognize the net11.0 preview moniker** — the default out-of-process toolchain crashes in SDK validation. The benchmarks bake `InProcessEmitToolchain` into their config; revisit when BDN learns .NET 11.
- **.NET 11 escape analysis stack-allocates single-frame boxes** — a micro-benchmark that constructs and consumes a boxing type in one frame reports 0 B allocated. Force escape with `[MethodImpl(MethodImplOptions.NoInlining)]` factory boundaries when the design question is about values that cross method boundaries.

## House Style

- **Project names are brand-free** (`src/Primitives/Primitives.csproj`); `Directory.Build.props` injects `AssemblyName`/`RootNamespace` as `Norse.$(MSBuildProjectName)`. The brand prefix lives in that one file so a fork rebrands without renaming a single project — build derivatives (`InternalsVisibleTo` via `$(AssemblyName)`) follow automatically; `namespace Norse.*` declarations in code stay until the fork deliberately culls them.

Tabs (except YAML/MD per .editorconfig) · `var` for returns, explicit type + `new()` for construction · omit default accessibility modifiers · XML docs mandatory on all public src members (CS1591 is an error in src) · test classes `public sealed`, test methods omit access modifiers · Shouldly/Xunit usings are global (injected via tests props — never add them per-file).

**Test naming — two accepted forms, chosen by what's under test:** `Should_{behavior}_when_{condition}` for scalar-parser tests (`Primitives.Tests` — a parse outcome is a behavior/condition pair, e.g. `Should_fail_with_malformed_reason_when_input_is_unrecognized`). `{Action}_{observed_behavior}` for forward-only cursor/reader tests (`Primitives.Ingestion.Tests` — e.g. `Read_throws_on_a_corrupt_workbook`), where the subject under test is a stateful sequence of operations rather than a single input→outcome pair and reads more clearly as a direct description than forced into `Should_when_`. Pick whichever form fits the project's existing files; don't mix both within one test class.

## Deferred Increments (pathway-proof spec §7 ledger — in rough order)

Five increments are landed: (1) `Result<T>` + `BooleanParser`; (2) gateway + combinators; (3) numeric family, `char`, `Guid`; (4) temporal parsers (`DateOnly`/`TimeOnly`/`DateTimeOffset`/`DateTime`/`TimeSpan`); (5) `TimeZoneParser` + `TemporalFusion` (DST-safe fusion of ISO date + ISO time + IANA zone → UTC `DateTime`). Remaining deferred items:

1. **`Combine`, async combinator siblings, `*Present` variants** — carry-forward from the pathway ledger; the fusion consumer proved fusion-with-a-failing-combiner is not `Combine`, so the deferral stands awaiting its own consumer. Spec: `../Glitnir/docs/Svartalfheim/specs/2026-06-11-svartalfheim-pathway-proof-design.md` §7.
2. **Caller-declared DST resolution door** (`Earliest`/`Latest`/`Reject`) — `TemporalFusion` off-default door for callers that legitimately resolve an ambiguous wall-clock by policy rather than re-prompt. Spec: `../Glitnir/docs/Svartalfheim/specs/2026-06-17-svartalfheim-temporal-fusion-design.md` §11.
3. **Pre-parsed fusion overload** — `Fuse(DateOnly, TimeOnly, TimeZoneInfo)` for ingress paths whose fields arrive in a declared non-ISO format and were parsed through `ParseExact` doors first. Spec: §11 ibid.
4. **Collect-all failure aggregation** — when an ingress path genuinely needs every bad field reported at once (first-failure-wins does not serve it); likely a validation-layer concern above the forge. Spec: §11 ibid.
5. **NuGet packaging metadata** — when something consumes the package.

Each increment is spec-first: brainstorm → spec → plan → code, with explicit human greenlights at each transition. **Every plan's REQUIRED SUB-SKILL line names `superpowers:subagent-driven-development` as the default — not a recommendation among equals; `superpowers:executing-plans` is the narrow fallback for a separate session with human review checkpoints — paired with `superpowers:test-driven-development`** — the orchestration skill sequences tasks, TDD governs how each one is actually coded. The forge's existing tests already hold this line in spirit (real-code assertions, no mocks, one behavior per test); naming the skill explicitly keeps it that way as the increments get harder.
